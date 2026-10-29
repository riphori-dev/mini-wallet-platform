using FluentValidation;
using MiniWallet.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniWallet.Application.Validations
{
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
}
