using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniWallet.Application.Validations
{
    public sealed class HistoryValidator : AbstractValidator<HistoryRequest>
    {
        public HistoryValidator()
        {
            RuleFor(x => x.PlayerId).NotEmpty();
            RuleFor(x => x.Currency).Must(x => x is "USD" or "EUR" or "BTC");
            RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }
}
