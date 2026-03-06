using FluentValidation;
using RealtimeAuction.Api.Dtos.Payment;

namespace RealtimeAuction.Api.Validators.Payment;

public class CreateDepositRequestValidator : AbstractValidator<CreateDepositRequest>
{
    public CreateDepositRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(2000).WithMessage("Số tiền nạp tối thiểu 2,000đ.")
            .LessThanOrEqualTo(100_000_000).WithMessage("Số tiền nạp tối đa 100,000,000đ.");
    }
}
