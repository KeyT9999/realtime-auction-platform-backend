using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email khong duoc de trong.")
            .EmailAddress().WithMessage("Email khong dung dinh dang.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mat khau khong duoc de trong.");

        RuleFor(x => x.CaptchaToken)
            .NotEmpty().WithMessage("CAPTCHA token khong duoc de trong.");
    }
}
