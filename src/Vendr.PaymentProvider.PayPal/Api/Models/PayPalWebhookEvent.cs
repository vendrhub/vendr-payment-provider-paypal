using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vendr.PaymentProvider.PayPal.Api.Models
{
    public class PayPalWebhookEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("event_type")]
        public string EventType { get; set; }

        [JsonProperty("event_version")]
        public string EventVersion { get; set; }

        [JsonProperty("resource_type")]
        public string ResourceType { get; set; }

        [JsonProperty("resource_version")]
        public string ResourceVersion { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("resource")]
        public JObject Resource { get; set; }

        [JsonProperty("create_time")]
        public string CreateTime { get; set; }

        [JsonProperty("links")]
        public PayPalHateoasLink[] Links { get; set; }
    }
}
