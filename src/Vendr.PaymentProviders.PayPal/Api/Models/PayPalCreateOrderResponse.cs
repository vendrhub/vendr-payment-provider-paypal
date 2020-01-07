using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalCreateOrderResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("links")]
        public PayPalHateoasLink[] Links { get; set; }
    }
}
