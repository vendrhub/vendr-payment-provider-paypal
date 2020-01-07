using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalHateoasLink
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("rel")]
        public string Rel { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }
    }
}
