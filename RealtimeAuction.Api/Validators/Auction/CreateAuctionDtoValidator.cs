using FluentValidation;
using RealtimeAuction.Api.Dtos.Auction;

namespace RealtimeAuction.Api.Validators.Auction;

public class CreateAuctionDtoValidator : AbstractValidator<CreateAuctionDto>
{
    public CreateAuctionDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề không được để trống.")
            .MinimumLength(3).WithMessage("Tiêu đề phải có ít nhất 3 ký tự.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.");

        RuleFor(x => x.StartingPrice)
            .GreaterThanOrEqualTo(1000).WithMessage("Giá khởi điểm tối thiểu 1,000 VND.");

        RuleFor(x => x.ReservePrice)
            .GreaterThan(0).WithMessage("Giá tối thiểu phải lớn hơn 0.")
            .When(x => x.ReservePrice.HasValue);

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("Thời gian bắt đầu không được để trống.");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("Thời gian kết thúc không được để trống.")
            .GreaterThan(x => x.StartTime).WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

        RuleFor(x => x.Duration)
            .GreaterThanOrEqualTo(60).WithMessage("Thời lượng tối thiểu 60 phút (1 giờ).");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Danh mục không được để trống.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Sản phẩm không được để trống.");

        RuleFor(x => x.Images)
            .NotEmpty().WithMessage("Cần ít nhất 1 hình ảnh.")
            .Must(x => x.Count >= 1).WithMessage("Cần ít nhất 1 hình ảnh.")
            .Must(x => x.Count <= 5).WithMessage("Tối đa 5 hình ảnh.");

        RuleFor(x => x.BidIncrement)
            .GreaterThanOrEqualTo(1000).WithMessage("Bước giá tối thiểu 1,000 VND.");

        RuleFor(x => x.BuyoutPrice)
            .GreaterThan(0).WithMessage("Giá mua ngay phải lớn hơn 0.")
            .When(x => x.BuyoutPrice.HasValue);
    }
}
