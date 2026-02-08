namespace RealtimeAuction.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string toName, string subject, string htmlContent);
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string resetUrl);
    Task SendVerificationEmailAsync(string toEmail, string toName, string verificationToken, string verificationUrl);
    Task SendOtpEmailAsync(string toEmail, string toName, string otpCode);
    Task SendPasswordResetOtpEmailAsync(string toEmail, string toName, string otpCode);
    Task SendWelcomeEmailAsync(string toEmail, string toName);
    
    // Auction Email Notifications
    Task SendAuctionEndingSoonEmailAsync(string toEmail, string toName, 
        string auctionTitle, string timeRemaining, string currentPrice, string auctionUrl);
    
    Task SendOutbidNotificationEmailAsync(string toEmail, string toName, 
        string auctionTitle, string yourBid, string newBid, string suggestedBid, string auctionUrl);
    
    Task SendAuctionWonEmailAsync(string toEmail, string toName, 
        string auctionTitle, string winningBid, string transactionUrl);
    
    Task SendBuyoutBuyerEmailAsync(string toEmail, string toName, 
        string auctionTitle, string buyoutPrice, string transactionUrl);
    
    Task SendBuyoutSellerEmailAsync(string toEmail, string toName, 
        string auctionTitle, string buyoutPrice, string buyerName, string transactionUrl);
    
    Task SendBidAcceptedEmailAsync(string toEmail, string toName, 
        string auctionTitle, string acceptedPrice, string sellerName, string transactionUrl);
    
    Task SendTransactionCompletedEmailAsync(string toEmail, string toName, 
        string role, string auctionTitle, string finalAmount, string transactionDate);
    
    Task SendTransactionReminderEmailAsync(string toEmail, string toName, 
        string auctionTitle, string status, int daysRemaining, string transactionUrl, string warningMessage);
    
    // Order Email Notifications
    Task SendOrderShippedEmailAsync(string toEmail, string toName, 
        string productTitle, string? trackingNumber, string? shippingCarrier);
}
