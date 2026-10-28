using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using MiniWallet.Domain;

namespace MiniWallet.Application;

public sealed record TransactionRequest(string ExternalTransactionId, Guid PlayerId, string Currency,
    string Type, string Amount, string? BetExternalTransactionId = null);
public sealed record TransactionCommand(string ProviderId, string ExternalTransactionId, Guid PlayerId,
    Currency Currency, string Type, Money Amount, string? BetExternalTransactionId);
public sealed record TransactionResponse(Guid TransactionId, string ExternalTransactionId, Guid PlayerId,
    string Currency, string Type, string Amount, string BalanceAfter, DateTimeOffset CreatedAt,
    string? BetExternalTransactionId);
public sealed record StoredResponse(int Status, string Body, string ContentType = "application/json");
public sealed record TransactionProblem(string Type, string Title, int Status, string Detail, string Code, string TraceId);
public sealed class WalletFailure(int status, string code, string message,
    Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}
public interface ITransactionStore
{
    Task<StoredResponse> ExecuteAsync(TransactionCommand command, ITransactionHandler handler, string traceId, CancellationToken ct);
}
public interface ITransactionHandler
{
    string Type { get; }
    FinancialEntry Apply(PlayerWallet wallet, TransactionCommand command, FinancialEntry? bet, bool settled, DateTimeOffset now);
}
public sealed class BetHandler : ITransactionHandler
{
    public string Type => "BET";
    public FinancialEntry Apply(PlayerWallet wallet, TransactionCommand command, FinancialEntry? bet, bool settled, DateTimeOffset now)
    {
        wallet.Bet(command.Amount);
        return FinancialEntry.Record(wallet, command.ProviderId, command.ExternalTransactionId, Type, command.Amount, now);
    }
}
public sealed class WinHandler : ITransactionHandler
{
    public string Type => "WIN";
    public FinancialEntry Apply(PlayerWallet wallet, TransactionCommand command, FinancialEntry? bet, bool settled, DateTimeOffset now)
    {
        wallet.Win(command.Amount, bet, command.ProviderId, settled);
        return FinancialEntry.Record(wallet, command.ProviderId, command.ExternalTransactionId, Type, command.Amount, now, bet!.Id);
    }
}
public static class AmountText
{
    public static bool TryParse(string? code, string? text, out Money? money)
    {
        money = null;
        if (text is null || text.Length > 29 || !Regex.IsMatch(text, "\\A[0-9]+(\\.[0-9]+)?\\z")) return false;
        try
        {
            var currency = Currency.Parse(code ?? "");
            var dot = text.IndexOf('.');
            if (dot >= 0 && text.Length - dot - 1 > currency.Scale) return false;
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return false;
            money = Money.Amount(currency, value);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FinancialRuleException) { return false; }
    }
}
public sealed class TransactionValidator : AbstractValidator<TransactionRequest>
{
    private const string IdPattern = "\\A[A-Za-z0-9._:-]{1,128}\\z";
    public TransactionValidator()
    {
        RuleFor(x => x.ExternalTransactionId).NotEmpty().Matches(IdPattern);
        RuleFor(x => x.PlayerId).NotEmpty();
        RuleFor(x => x.Currency).Must(x => x is "USD" or "EUR" or "BTC");
        RuleFor(x => x.Type).Must(x => x is "BET" or "WIN");
        RuleFor(x => x.Amount).Must((request, amount) => AmountText.TryParse(request.Currency, amount, out _))
            .WithMessage("Positive invariant decimal string required; USD/EUR scale <=2, BTC <=8, maximum 99999999999999999999.99999999.");
        When(x => x.Type == "WIN", () => RuleFor(x => x.BetExternalTransactionId).NotEmpty().Matches(IdPattern));
        When(x => x.Type == "BET", () => RuleFor(x => x.BetExternalTransactionId).Null());
    }
}
public sealed class SubmitTransaction(IValidator<TransactionRequest> validator, ITransactionStore store,
    IEnumerable<ITransactionHandler> handlers)
{
    public async Task<StoredResponse> ExecuteAsync(string providerId, TransactionRequest request, string traceId, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) throw new WalletFailure(400, "invalid_request", "Invalid transaction.",
            validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Length > 128)
            throw new WalletFailure(403, "forbidden", "Valid provider identity required.");
        AmountText.TryParse(request.Currency, request.Amount, out var amount);
        var command = new TransactionCommand(providerId, request.ExternalTransactionId, request.PlayerId,
            Currency.Parse(request.Currency), request.Type, amount!, request.BetExternalTransactionId);
        return await store.ExecuteAsync(command, handlers.Single(h => h.Type == request.Type), traceId, ct);
    }
}

