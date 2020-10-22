using Newtonsoft.Json;
using System;

namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    // https://developer.paypal.com/docs/api/orders/v2/#definition-order_application_context

    public class PayPalOrderApplicationContext
    {
        [JsonProperty("brand_name")]
        public string BrandName { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("landing_page")]
        public string LandingPage { get; set; }

        [JsonProperty("shipping_preference")]
        public string ShippingPreference { get; set; }

        [JsonProperty("user_action")]
        public string UserAction { get; set; }

        [JsonProperty("return_url")]
        public string ReturnUrl { get; set; }

        [JsonProperty("cancel_url")]
        public string CancelUrl { get; set; }

        public PayPalOrderApplicationContext()
        {
            LandingPage = "NO_PREFERENCE";
            ShippingPreference = "GET_FROM_FILE";
            UserAction = "CONTINUE";
        }
    }
}
