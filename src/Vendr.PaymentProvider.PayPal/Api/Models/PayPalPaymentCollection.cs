using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalPaymentCollection
    {
        [JsonProperty("authorizations")]
        public PayPalAuthorizationPayment[] Authorizations { get; set; }

        [JsonProperty("captures")]
        public PayPalCapturePayment[] Captures { get; set; }

        [JsonProperty("refunds")]
        public PayPalRefundPayment[] Refunds { get; set; }

    }
}
