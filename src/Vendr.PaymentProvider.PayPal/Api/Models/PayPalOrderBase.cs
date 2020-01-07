using Newtonsoft.Json;

namespace Vendr.PaymentProvider.PayPal.Api.Models
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
