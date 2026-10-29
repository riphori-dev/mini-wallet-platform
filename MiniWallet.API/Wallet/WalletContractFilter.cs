using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniWallet.API.Wallet;

internal sealed class WalletContractFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        var write = document.Paths["/api/v1/transactions"].Operations!.Values.Single();
        write.Summary = "Provider BET/WIN with durable transaction identity";
        write.Description = """
            Requires a verified **provider machine JWT**, scope transactions:write. Provider ID is read from token claims.
            Amount is a positive **invariant decimal string**, never a JSON number. USD/EUR: <=2 fractional digits;
            BTC: <=8. Maximum amount/balance 99999999999999999999.99999999 (USD/EUR effective .99).
            No signs, exponent, grouping, whitespace or excess precision (including trailing zeroes). No silent rounding.

            BET debits available balance. WIN requires betExternalTransactionId for an accepted BET by the
            same provider/player/currency; amount is the positive payout and may differ from stake.
            **Assumption: one successful WIN per BET**, enforced by a database unique constraint.

            Example WIN after the example BET:
            `{"externalTransactionId":"demo-win-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"WIN","amount":"25.00","betExternalTransactionId":"demo-bet-001"}`

            201 means committed. Key=(providerId,externalTransactionId) across all wallets/currencies. Exact typed replay returns
            original status/body, including original balanceAfter. Valid "10" and "10.00" compare equal. Changed payload: 409
            idempotency_conflict. Terminal 409 business rejections reserve the ID forever in this assessment; after correction use a new ID.
            Missing wallet 404 and validation/auth failures do not reserve the key. Transient/lock failures: retryable 503 + Retry-After.
            A timeout/unknown commit requires retry with **the same ID and typed payload**; never assume success before commit.
            Database lock wait is bounded to 3 seconds. Do not automatically retry a terminal 409 as a new transaction.

            Success JSON: transactionId, externalTransactionId, playerId, currency, type, amount, balanceAfter,
            createdAt (UTC ISO 8601), betExternalTransactionId (null for BET). Amount/balance are fixed-scale strings.
            Errors use application/problem+json with type/title/status/detail/code/traceId. Statuses:
            400 invalid input; 401 missing/invalid JWT; 403 wrong role/scope; 404 wallet_unavailable;
            409 insufficient_funds/bet_not_found/bet_mismatch/bet_already_settled/balance_limit_exceeded/idempotency_conflict;
            503 transient_unavailability. Unexpected failures are sanitized 500; uncertain writes still require same-ID retry.

            Development demo only: demo@miniwallet.test / Local-demo-password-123!, player ID shown in example.
            Explicit DEV_FUNDING ledger entries open USD=100.00, EUR=100.00, BTC=0.01000000 once.
            Ordinary registrations start at zero. No public deposit route exists. Use a new external ID for each intended action.
            """;
        foreach (var media in write.RequestBody!.Content!.Values)
            media.Example = JsonNode.Parse("""
                {"externalTransactionId":"demo-bet-001","playerId":"de000000-0000-4000-8000-000000000001","currency":"USD","type":"BET","amount":"10.00","betExternalTransactionId":null}
                """);
        foreach (var path in document.Paths.Where(x => x.Key.EndsWith("/transactions") && x.Key != "/api/v1/transactions"))
        {
            var operation = path.Value.Operations!.Values.Single();
            operation.Description = """
                Player /me routes derive ownership only from verified JWT sub; admin /players/{playerId} routes require admin role.
                Providers cannot read either. History includes immutable accepted BET/WIN and explicit DEV_FUNDING entries, never rejections.
                Query page defaults 1 (range 1–10000), pageSize defaults 20 (range 1–100).
                Order createdAt DESC then transactionId DESC; response {items,page,pageSize,hasMore}.
                Offset pages are deterministic for unchanged data; new commits between requests can shift page boundaries.
                Amount and balanceAfter are fixed-scale decimal strings. Missing/unprovisioned wallet: 404; bad query: 400.
                """;
        }
        foreach (var path in document.Paths)
            foreach (var operation in path.Value.Operations!.Values)
            {
                foreach (var response in operation.Responses ?? [])
                {
                    if (!int.TryParse(response.Key, out var status) || status < 400) continue;
                    if (response.Value is not OpenApiResponse concrete) continue;
                    var schema = concrete.Content?.Values.FirstOrDefault()?.Schema;
                    concrete.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = schema }
                    };
                }
            }
    }
}

