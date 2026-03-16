using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email khong duoc de trong.")
            .EmailAddress().WithMessage("Email khong dung dinh dang.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mat khau khong duoc de trong.")
            .MinimumLength(6).WithMessage("Mat khau phai co it nhat 6 ky tu.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ho ten khong duoc de trong.")
            .MaximumLength(100).WithMessage("Ho ten khong duoc vuot qua 100 ky tu.");

        RuleFor(x => x.CaptchaToken)
            .NotEmpty().WithMessage("CAPTCHA token khong duoc de trong.");
    }
}
