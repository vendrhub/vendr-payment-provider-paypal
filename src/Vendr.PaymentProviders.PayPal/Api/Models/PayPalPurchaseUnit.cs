using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalPurchaseUnit : PayPalPurchaseUnitBase
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("payments")]
        public PayPalPaymentCollection Payments { get; set; }
    }
}
