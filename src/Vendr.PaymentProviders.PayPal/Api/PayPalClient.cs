using Flurl.Http;
using Flurl.Http.Content;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vendr.PaymentProviders.PayPal.Api.Models;

namespace Vendr.PaymentProviders.PayPal.Api
{
    public class PayPalClient
    {
        private static MemoryCache AccessTokenCache = new MemoryCache("PayPalClient_AccessTokenCache");

        public const string SandboxApiUrl = "https://api.sandbox.paypal.com";

        public const string LiveApiUrl = "https://api.paypal.com";

        private PayPalClientConfig _config;

        public PayPalClient(PayPalClientConfig config)
        {
            _config = config;
        }

        public async Task<PayPalOrder> CreateOrderAsync(PayPalCreateOrderRequest request)
        {
            return await RequestAsync("/v2/checkout/orders", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .PostJsonAsync(request)
                .ReceiveJson<PayPalOrder>());
        }

        public async Task<PayPalOrder> GetOrderAsync(string orderId)
        {
            return await RequestAsync($"/v2/checkout/orders/{orderId}", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .GetAsync()
                .ReceiveJson<PayPalOrder>());
        }

        public async Task<PayPalOrder> AuthorizeOrderAsync(string orderId)
        {
            return await RequestAsync($"/v2/checkout/orders/{orderId}/authorize", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .PostJsonAsync(null)
                .ReceiveJson<PayPalOrder>());
        }

        public async Task<PayPalOrder> CaptureOrderAsync(string orderId)
        {
            return await RequestAsync($"/v2/checkout/orders/{orderId}/capture", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .PostJsonAsync(null)
                .ReceiveJson<PayPalOrder>());
        }

        public async Task<PayPalCapturePayment> CapturePaymentAsync(string paymentId)
        {
            return await RequestAsync($"/v2/payments/authorizations/{paymentId}/capture", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .PostJsonAsync(new
                {
                    final_capture = true
                })
                .ReceiveJson<PayPalCapturePayment>());
        }

        public async Task<PayPalRefundPayment> RefundPaymentAsync(string paymentId)
        {
            return await RequestAsync($"/v2/payments/captures/{paymentId}/refund", async (req) => await req
                .PostJsonAsync(null)
                .ReceiveJson<PayPalRefundPayment>());
        }

        public async Task CancelPayment(string paymentId)
        {
            await RequestAsync($"/v2/payments/authorizations/{paymentId}/void", async (req) => await req
                .WithHeader("Prefer", "return=representation")
                .PostJsonAsync(null));
        }

        public async Task<PayPalWebhookEvent> ParseWebhookEventAsync(HttpRequestMessage request)
        {
            var payPalWebhookEvent = default(PayPalWebhookEvent);

            var headers = request.Headers;

            using (var stream = await request.Content.ReadAsStreamAsync())
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();

                    var webhookSignatureRequest = new PayPalVerifyWebhookSignatureRequest
                    {
                        AuthAlgorithm = headers.GetValues("paypal-auth-algo").FirstOrDefault(),
                        CertUrl = headers.GetValues("paypal-cert-url").FirstOrDefault(),
                        TransmissionId = headers.GetValues("paypal-transmission-id").FirstOrDefault(),
                        TransmissionSignature = headers.GetValues("paypal-transmission-sig").FirstOrDefault(),
                        TransmissionTime = headers.GetValues("paypal-transmission-time").FirstOrDefault(),
                        WebhookId = _config.WebhookId,
                        WebhookEvent = new { }
                    };

                    var webhookSignatureRequestStr = JsonConvert.SerializeObject(webhookSignatureRequest).Replace("{}", json);

                    var content = new CapturedStringContent(webhookSignatureRequestStr, Encoding.UTF8, "application/json");

                    var result = await RequestAsync("/v1/notifications/verify-webhook-signature", async (req) => await req
                        .WithHeader("Content-Type", "application/json")
                        .SendAsync(HttpMethod.Post, content)
                        .ReceiveJson<PayPalVerifyWebhookSignatureResult>());

                    if (result != null && result.VerificationStatus == "SUCCESS")
                    {
                        payPalWebhookEvent = JsonConvert.DeserializeObject<PayPalWebhookEvent>(json);
                    }
                }
            }

            return payPalWebhookEvent;
        }

        private async Task<TResult> RequestAsync<TResult>(string url, Func<IFlurlRequest, Task<TResult>> func)
        {
            var result = default(TResult);

            try
            {
                var accessToken = await GetAccessTokenAsync();
                var req = new FlurlRequest(_config.BaseUrl + url)
                    .WithOAuthBearerToken(accessToken);

                result = await func.Invoke(req);
            }
            catch (FlurlHttpException ex)
            {
                if (ex.Call.HttpStatus == HttpStatusCode.Unauthorized)
                {
                    var accessToken = await GetAccessTokenAsync(true);
                    var req = new FlurlRequest(_config.BaseUrl + url)
                        .WithOAuthBearerToken(accessToken);

                    result = await func.Invoke(req);
                }
                else
                {
                    throw;
                }
            }

            return result;
        }

        private async Task<string> GetAccessTokenAsync(bool forceReAuthentication = false)
        {
            var cacheKey = $"{_config.BaseUrl}__{_config.ClientId}__{_config.Secret}";

            if (!AccessTokenCache.Contains(cacheKey) || forceReAuthentication)
            {
                var result = await AuthenticateAsync();

                AccessTokenCache.Set(cacheKey, result.AccessToken, new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn - 5)
                });
            }

            return AccessTokenCache.Get(cacheKey).ToString();
        }

        private async Task<PayPalAccessTokenResult> AuthenticateAsync()
        {
            return await new FlurlRequest(_config.BaseUrl + "/v1/oauth2/token")
                .WithBasicAuth(_config.ClientId, _config.Secret)
                .PostUrlEncodedAsync(new { grant_type = "client_credentials" })
                .ReceiveJson<PayPalAccessTokenResult>();
        }
    }
}
