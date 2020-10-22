namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class SandboxPayPalClientConfig : PayPalClientConfig
    {
        public override string BaseUrl => PayPalClient.SandboxApiUrl;
        public override string IpnUrl => PayPalClient.SandboxIpnUrl;
    }
}
