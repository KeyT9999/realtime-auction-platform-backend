using System.Text.Json.Serialization;

namespace RealtimeAuction.Api.Dtos.Payment;

public class PayOsWebhookPayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public WebhookData? Data { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = null!;
}

public class WebhookData
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = null!;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = null!;

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; set; } = null!;

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; } = null!;

    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = null!;

    [JsonPropertyName("counterAccountBankId")]
    public string? CounterAccountBankId { get; set; }

    [JsonPropertyName("counterAccountBankName")]
    public string? CounterAccountBankName { get; set; }

    [JsonPropertyName("counterAccountName")]
    public string? CounterAccountName { get; set; }

    [JsonPropertyName("counterAccountNumber")]
    public string? CounterAccountNumber { get; set; }

    [JsonPropertyName("virtualAccountName")]
    public string? VirtualAccountName { get; set; }

    [JsonPropertyName("virtualAccountNumber")]
    public string? VirtualAccountNumber { get; set; }
}
