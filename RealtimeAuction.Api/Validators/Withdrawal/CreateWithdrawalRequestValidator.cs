using FluentValidation;
using RealtimeAuction.Api.Dtos.Withdrawal;

namespace RealtimeAuction.Api.Validators.Withdrawal;

public class CreateWithdrawalRequestValidator : AbstractValidator<CreateWithdrawalRequest>
{
    public CreateWithdrawalRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(50_000).WithMessage("Số tiền rút tối thiểu là 50,000 VND.");

        RuleFor(x => x.BankAccountId)
            .NotEmpty().WithMessage("Vui lòng chọn tài khoản ngân hàng.");
    }
}
