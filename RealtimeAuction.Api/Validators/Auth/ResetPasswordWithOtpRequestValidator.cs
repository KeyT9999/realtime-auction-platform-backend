using FluentValidation;
using RealtimeAuction.Api.Dtos.Auth;

namespace RealtimeAuction.Api.Validators.Auth;

public class ResetPasswordWithOtpRequestValidator : AbstractValidator<ResetPasswordWithOtpRequest>
{
    public ResetPasswordWithOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải có đúng 6 ký tự.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.");
    }
}
