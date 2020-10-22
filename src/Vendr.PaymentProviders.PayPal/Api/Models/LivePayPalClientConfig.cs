namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class LivePayPalClientConfig : PayPalClientConfig
    {
        public override string BaseUrl => PayPalClient.LiveApiUrl;
        public override string IpnUrl => PayPalClient.LiveIpnUrl;
    }
}
