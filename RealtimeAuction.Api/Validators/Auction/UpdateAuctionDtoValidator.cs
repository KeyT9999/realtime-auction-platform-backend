using FluentValidation;
using RealtimeAuction.Api.Dtos.Auction;

namespace RealtimeAuction.Api.Validators.Auction;

public class UpdateAuctionDtoValidator : AbstractValidator<UpdateAuctionDto>
{
    public UpdateAuctionDtoValidator()
    {
        RuleFor(x => x.Title)
            .MinimumLength(3).WithMessage("Tiêu đề phải có ít nhất 3 ký tự.")
            .MaximumLength(200).WithMessage("Tiêu đề không được vượt quá 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.StartingPrice)
            .GreaterThan(0).WithMessage("Giá khởi điểm phải lớn hơn 0.")
            .When(x => x.StartingPrice.HasValue);

        RuleFor(x => x.ReservePrice)
            .GreaterThan(0).WithMessage("Giá tối thiểu phải lớn hơn 0.")
            .When(x => x.ReservePrice.HasValue);

        RuleFor(x => x.Duration)
            .GreaterThanOrEqualTo(1).WithMessage("Thời lượng phải ít nhất 1 phút.")
            .When(x => x.Duration.HasValue);

        RuleFor(x => x.BidIncrement)
            .GreaterThan(0).WithMessage("Bước giá phải lớn hơn 0.")
            .When(x => x.BidIncrement.HasValue);

        RuleFor(x => x.BuyoutPrice)
            .GreaterThan(0).WithMessage("Giá mua ngay phải lớn hơn 0.")
            .When(x => x.BuyoutPrice.HasValue);

        RuleFor(x => x.Images)
            .Must(x => x!.Count <= 5).WithMessage("Tối đa 5 hình ảnh.")
            .When(x => x.Images != null);
    }
}
