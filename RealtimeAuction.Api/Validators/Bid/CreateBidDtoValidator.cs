using FluentValidation;
using RealtimeAuction.Api.Dtos.Bid;

namespace RealtimeAuction.Api.Validators.Bid;

public class CreateBidDtoValidator : AbstractValidator<CreateBidDto>
{
    public CreateBidDtoValidator()
    {
        RuleFor(x => x.AuctionId)
            .NotEmpty().WithMessage("Mã phiên đấu giá không được để trống.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền đặt giá phải lớn hơn 0.");
    }
}
