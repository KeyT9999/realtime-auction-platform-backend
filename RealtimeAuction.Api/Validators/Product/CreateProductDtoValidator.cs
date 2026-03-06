using FluentValidation;
using RealtimeAuction.Api.Dtos.Product;

namespace RealtimeAuction.Api.Validators.Product;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống.")
            .MinimumLength(2).WithMessage("Tên sản phẩm phải có ít nhất 2 ký tự.")
            .MaximumLength(200).WithMessage("Tên sản phẩm không được vượt quá 200 ký tự.");

        RuleFor(x => x.Year)
            .InclusiveBetween(1900, 2100).WithMessage("Năm sản xuất phải từ 1900 đến 2100.")
            .When(x => x.Year.HasValue);
    }
}
