using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    // https://developer.paypal.com/docs/api/webhooks/v1/#verify-webhook-signature

    public class PayPalVerifyWebhookSignatureRequest
    {
        [JsonProperty("auth_algo")]
        public string AuthAlgorithm { get; set; }

        [JsonProperty("cert_url")]
        public string CertUrl { get; set; }

        [JsonProperty("transmission_id")]
        public string TransmissionId { get; set; }

        [JsonProperty("transmission_sig")]
        public string TransmissionSignature { get; set; }

        [JsonProperty("transmission_time")]
        public string TransmissionTime { get; set; }

        [JsonProperty("webhook_id")]
        public string WebhookId { get; set; }

        [JsonProperty("webhook_event")]
        public object WebhookEvent { get; set; }
    }
}
