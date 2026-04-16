// Mục đích tệp: Trien khai logic/chuc nang chinh cua file VerifyEmailRequestValidator.
using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token xác thực không được để trống.");
    }
}
