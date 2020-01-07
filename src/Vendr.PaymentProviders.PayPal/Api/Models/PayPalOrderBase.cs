using Newtonsoft.Json;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalOrderBase<TPaymentUnit>
        where TPaymentUnit : PayPalPurchaseUnitBase
    {
        [JsonProperty("intent")]
        public string Intent { get; set; }

        [JsonProperty("purchase_units")]
        public TPaymentUnit[] PurchaseUnits { get; set; }

        [JsonProperty("application_context")]
        public PayPalOrderApplicationContext AplicationContext { get; set; }
    }
}
