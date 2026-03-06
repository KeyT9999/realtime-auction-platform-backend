using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");
    }
}
