using Newtonsoft.Json;

namespace Vendr.PaymentProvider.PayPal.Api.Models
{
    public class PayPalAmount
    {
        [JsonProperty("currency_code")]
        public string CurrencyCode { get; set; }

        [JsonProperty("value")]
        public decimal Value { get; set; }
    }
}
