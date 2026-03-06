using FluentValidation;
using RealtimeAuction.Api.Dtos.Admin;

namespace RealtimeAuction.Api.Validators.Admin;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MinimumLength(2).WithMessage("Họ tên phải có ít nhất 2 ký tự.");

        RuleFor(x => x.Role)
            .Must(r => r == "User" || r == "Admin")
            .WithMessage("Role phải là 'User' hoặc 'Admin'.");
    }
}
