using MiniWallet.Application.Services;
using MiniWallet.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniWallet.Application.Interfaces
{
    public interface ITransactionStore
    {
        Task<StoredResponse> ExecuteAsync(TransactionCommand command, ITransactionHandler handler, string traceId, CancellationToken ct);
    }
    public interface ITransactionHandler
    {
        string Type { get; }
        FinancialEntry Apply(PlayerWallet wallet, TransactionCommand command, FinancialEntry? bet, bool settled, DateTimeOffset now);
    }
}
