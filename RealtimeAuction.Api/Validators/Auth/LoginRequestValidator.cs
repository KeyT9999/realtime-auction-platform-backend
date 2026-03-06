using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}
