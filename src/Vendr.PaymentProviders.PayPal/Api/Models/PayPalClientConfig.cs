namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public abstract class PayPalClientConfig
    {
        public string ClientId { get; set; }
        public string Secret { get; set; }
        public string WebhookId { get; set; }
        public abstract string BaseUrl { get; }
    }
}
