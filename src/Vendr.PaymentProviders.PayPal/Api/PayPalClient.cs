using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using Vendr.PaymentProviders.PayPal.Api.Models;

namespace Vendr.PaymentProviders.PayPal.Api
{
    public class PayPalClient
    {
        private static MemoryCache AccessTokenCache = new MemoryCache("PayPalClient_AccessTokenCache");

        public const string SanboxApiUrl = "https://api.sandbox.paypal.com";

        public const string LiveApiUrl = "https://api.paypal.com";

        private PayPalClientConfig _config;

        public PayPalClient(PayPalClientConfig config)
        {
            _config = config;
        }

        public PayPalCreateOrderResponse CreateOrder(PayPalCreateOrderRequest request)
        {
            return Request("/v2/checkout/orders", (req) => req
                .PostJsonAsync(request)
                .ReceiveJson<PayPalCreateOrderResponse>());
        }

        public PayPalWebhookEvent ParseWebhookEvent(HttpRequestBase request, string webhookId)
        {
            var payPalWebhookEvent = default(PayPalWebhookEvent);

            if (request.InputStream.CanSeek)
                request.InputStream.Seek(0, SeekOrigin.Begin);

            using (var reader = new StreamReader(request.InputStream))
            {
                var json = reader.ReadToEnd();

                var tmpPayPalWebhookEvent = JsonConvert.DeserializeObject<PayPalWebhookEvent>(json);

                var result = Request("/v1/notifications/verify-webhook-signature", (req) => req
                    .PostJsonAsync(new PayPalVerifyWebhookSignatureRequest
                    {
                        AuthAlgorithm = request.Headers["paypal-auth-algo"],
                        CertUrl = request.Headers["paypal-cert-url"],
                        TransmissionId = request.Headers["paypal-transmission-id"],
                        TransmissionSignature = request.Headers["paypal-transmission-sig"],
                        TransmissionTime = request.Headers["paypal-transmission-time"],
                        WebhookId = webhookId,
                        WebhookEvent = tmpPayPalWebhookEvent
                    })
                    .ReceiveJson<PayPalVerifyWebhookSignatureResult>());

                if (result != null && result.VerificationStatus == "SUCCESS")
                {
                    payPalWebhookEvent = tmpPayPalWebhookEvent;
                }
            }

            return payPalWebhookEvent;
        }

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

        private TResult Request<TResult>(string url, Func<IFlurlRequest, Task<TResult>> func)
        {
            var result = default(TResult);

            try
            {
                var req = new FlurlRequest(_config.BaseUrl + url)
                    .WithOAuthBearerToken(GetAccessToken());

                result = func.Invoke(req).Result;
            }
            catch (FlurlHttpException ex)
            {
                if (ex.Call.HttpStatus == HttpStatusCode.Unauthorized)
                {
                    var req = new FlurlRequest(_config.BaseUrl + url)
                        .WithOAuthBearerToken(GetAccessToken(true));

                    result = func.Invoke(req).Result;
                }
                else
                {
                    throw;
                }
            }

            return result;
        }

        private string GetAccessToken(bool forceReAuthentication = false)
        {
            var cacheKey = $"{_config.BaseUrl}__{_config.ClientId}__{_config.Secret}";

            if (!AccessTokenCache.Contains(cacheKey) || forceReAuthentication)
            {
                var result = Authenticate();

                AccessTokenCache.Set(cacheKey, result.AccessToken, new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn - 5)
                });
            }

            return AccessTokenCache.Get(cacheKey).ToString();
        }

        private PayPalAccessTokenResult Authenticate()
        {
            return new FlurlRequest(_config.BaseUrl + "/v1/oauth2/token")
                .WithHeader("Accept", "x-www-form-urlencoded")
                .WithBasicAuth(_config.ClientId, _config.Secret)
                .PostUrlEncodedAsync(new { grant_type = "client_credentials" })
                .ReceiveJson<PayPalAccessTokenResult>()
                .Result;
        }
    }
}
