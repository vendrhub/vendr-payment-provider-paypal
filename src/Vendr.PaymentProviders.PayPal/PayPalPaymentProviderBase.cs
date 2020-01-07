using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.PaymentProviders.PayPal.Api.Models;

namespace Vendr.PaymentProviders.PayPal
{
    public abstract class PayPalPaymentProviderBase<TSettings> : PaymentProviderBase<TSettings>
        where TSettings : PayPalSettingsBase, new()
    {
        public PayPalPaymentProviderBase(VendrContext vendr)
            : base(vendr)
        { }

        public override string GetCancelUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.CancelUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.ContinueUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.ErrorUrl;
        }

        

        

        

        //protected PayPalWebhookEvent GetPayPalWebhookEvent(PayPalWebhookRequestConfig requestConfig, HttpRequestBase request)
        //{
        //    var payPalWebhookEvent = default(PayPalWebhookEvent);

        //    if (HttpContext.Current.Items["Vendr_PayPalWebhookEvent"] != null)
        //    {
        //        payPalWebhookEvent = (PayPalWebhookEvent)HttpContext.Current.Items["Vendr_PayPalWebhookEvent"];
        //    }
        //    else
        //    {
        //        payPalWebhookEvent = ParseAndValidatePayPalWebhookEvent(requestConfig, request);

        //        HttpContext.Current.Items["Vendr_PayPalWebhookEvent"] = payPalWebhookEvent;
        //    }

        //    return payPalWebhookEvent;
        //}

        //private PayPalWebhookEvent ParseAndValidatePayPalWebhookEvent(PayPalWebhookRequestConfig requestConfig, HttpRequestBase request)
        //{
        //    var payPalWebhookEvent = default(PayPalWebhookEvent);

        //    if (request.InputStream.CanSeek)
        //        request.InputStream.Seek(0, SeekOrigin.Begin);

        //    using (var reader = new StreamReader(request.InputStream))
        //    {
        //        var json = reader.ReadToEnd();

        //        var tmpPayPalWebhookEvent = JsonConvert.DeserializeObject<PayPalWebhookEvent>(json);

        //        var result = MakePayPalRequest("/v1/notifications/verify-webhook-signature", (req) => req
        //            .PostJsonAsync(new PayPalVerifyWebhookSignatureRequest
        //            {
        //                AuthAlgorithm = request.Headers["paypal-auth-algo"],
        //                CertUrl = request.Headers["paypal-cert-url"],
        //                TransmissionId = request.Headers["paypal-transmission-id"],
        //                TransmissionSignature = request.Headers["paypal-transmission-sig"],
        //                TransmissionTime = request.Headers["paypal-transmission-time"],
        //                WebhookId = requestConfig.WebhookId,
        //                WebhookEvent = tmpPayPalWebhookEvent
        //            })
        //            .ReceiveJson<PayPalVerifyWebhookSignatureResult>(),
        //            requestConfig);

        //        if (result != null && result.VerificationStatus == "SUCCESS")
        //        {
        //            payPalWebhookEvent =  tmpPayPalWebhookEvent;
        //        }
        //    }

        //    return payPalWebhookEvent;
        //}

        protected PayPalClientConfig GetPayPalClientConfig(PayPalSettingsBase settings)
        {
            return null;
            //var clientId = settings.Mode == PayPalPaymentProviderMode.Sandbox ? settings.SandboxClientId : settings.LiveClientId;
            //var secret = settings.Mode == PayPalPaymentProviderMode.Sandbox ? settings.SandboxSecret : settings.LiveSecret;
            //var webhookId = settings.Mode == PayPalPaymentProviderMode.Sandbox ? settings.SandboxWebhookId : settings.LiveWebhookId;
            //var apiBaseUrl = settings.Mode == PayPalPaymentProviderMode.Sandbox ? SanboxApiUrl : LiveApiUrl;

            //return new PayPalClientConfig
            //{
            //    ClientId = clientId,
            //    Secret = secret,
            //    WebhookId = webhookId,
            //    BaseUrl = apiBaseUrl
            //};
        }

        //protected static long DollarsToCents(decimal val)
        //{
        //    var cents = val * 100M;
        //    var centsRounded = Math.Round(cents, MidpointRounding.AwayFromZero);
        //    return Convert.ToInt64(centsRounded);
        //}

        //protected static decimal CentsToDollars(long val)
        //{
        //    return val / 100M;
        //}
    }
}
