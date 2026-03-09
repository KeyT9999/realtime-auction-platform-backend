namespace RealtimeAuction.Api.Models.Enums;

public enum DisputeReason
{
    ItemNotReceived = 0,    // Không nhận được hàng
    ItemNotAsDescribed = 1, // Không đúng mô tả
    ItemDamaged = 2,        // Hàng bị hỏng / vỡ
    WrongItem = 3,          // Giao nhầm hàng
    SellerNotShipping = 4,  // Người bán không giao hàng
    BuyerNotPaying = 5,     // Người mua không thanh toán
    FraudSuspected = 6,     // Nghi lừa đảo
    Other = 7               // Lý do khác
}
