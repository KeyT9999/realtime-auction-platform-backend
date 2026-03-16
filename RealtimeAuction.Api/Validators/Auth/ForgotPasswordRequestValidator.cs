using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email khong duoc de trong.")
            .EmailAddress().WithMessage("Email khong dung dinh dang.");

        RuleFor(x => x.CaptchaToken)
            .NotEmpty().WithMessage("CAPTCHA token khong duoc de trong.");
    }
}
