using System;
using System.Linq;
using Vendr.Core.Models;
using Vendr.Core.Api;
using Vendr.Core.PaymentProviders;
using Vendr.PaymentProviders.PayPal.Api.Models;
using Vendr.PaymentProviders.PayPal.Api;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vendr.Common.Logging;
using Vendr.Extensions;

namespace Vendr.PaymentProviders.PayPal
{
    [PaymentProvider("paypal-checkout-onetime", "PayPal Checkout (One Time)", "PayPal Checkout payment provider for one time payments")]
    public class PayPalCheckoutOneTimePaymentProvider : PayPalPaymentProviderBase<PayPalCheckoutOneTimeSettings>
    {
        private ILogger<PayPalCheckoutOneTimePaymentProvider> _logger;

        public PayPalCheckoutOneTimePaymentProvider(VendrContext vendr,
            ILogger<PayPalCheckoutOneTimePaymentProvider> logger)
            : base(vendr)
        {
            _logger = logger;
        }

        public override bool CanFetchPaymentStatus => true;
        public override bool CanCapturePayments => true;
        public override bool CanCancelPayments => true;
        public override bool CanRefundPayments => true;

        // Don't finalize at continue as we will finalize async via webhook
        public override bool FinalizeAtContinueUrl => false;

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new []{
            new TransactionMetaDataDefinition("PayPalOrderId", "PayPal Order ID")
        };

        public override async Task<OrderReference> GetOrderReferenceAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(ctx.Settings);
                var client = new PayPalClient(clientConfig);
                var payPalWebhookEvent = await GetPayPalWebhookEventAsync(client, ctx);

                if (payPalWebhookEvent != null)
                {
                    if (payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER."))
                    {
                        var payPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();
                        if (payPalOrder?.PurchaseUnits != null && payPalOrder.PurchaseUnits.Length == 1)
                        {
                            return OrderReference.Parse(payPalOrder.PurchaseUnits[0].CustomId);
                        }
                    }
                    else if (payPalWebhookEvent.EventType.StartsWith("PAYMENT."))
                    {
                        var payPalPayment = payPalWebhookEvent.Resource.ToObject<PayPalPayment>();
                        if (payPalPayment != null)
                        {
                            return OrderReference.Parse(payPalPayment.CustomId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - GetOrderReference");
            }

            return await base.GetOrderReferenceAsync(ctx);
        }

        public override async Task<PaymentFormResult> GenerateFormAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            // Get currency information
            var currency = Vendr.Services.CurrencyService.GetCurrency(ctx.Order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
            {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            // Create the order
            var clientConfig = GetPayPalClientConfig(ctx.Settings);
            var client = new PayPalClient(clientConfig);
            var payPalOrder = await client.CreateOrderAsync(new PayPalCreateOrderRequest
            {
                Intent = ctx.Settings.Capture 
                    ? PayPalOrder.Intents.CAPTURE 
                    : PayPalOrder.Intents.AUTHORIZE,
                PurchaseUnits = new[] 
                {
                    new PayPalPurchaseUnitRequest
                    {
                        CustomId = ctx.Order.GenerateOrderReference(),
                        Amount = new PayPalAmount
                        {
                            CurrencyCode = currencyCode,
                            Value = ctx.Order.TransactionAmount.Value.Value.ToString("0.00", CultureInfo.InvariantCulture)
                        }
                    }
                },
                AplicationContext = new PayPalOrderApplicationContext
                {
                    BrandName = ctx.Settings.BrandName,
                    UserAction = "PAY_NOW",
                    ReturnUrl = ctx.Urls.ContinueUrl,
                    CancelUrl = ctx.Urls.CancelUrl
                }
            });

            // Setup the payment form to redirect to approval link
            var approveLink = payPalOrder.Links.FirstOrDefault(x => x.Rel == "approve");
            var approveLinkMethod = (PaymentFormMethod)Enum.Parse(typeof(PaymentFormMethod), approveLink.Method, true);

            return new PaymentFormResult()
            {
                Form = new PaymentForm(approveLink.Href, approveLinkMethod)
            };
        }

        public override async Task<CallbackResult> ProcessCallbackAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(ctx.Settings);
                var client = new PayPalClient(clientConfig);
                var payPalWebhookEvent = await GetPayPalWebhookEventAsync(client, ctx);

                if (payPalWebhookEvent != null)
                {
                    var metaData = new Dictionary<string, string>();

                    PayPalOrder payPalOrder = null;
                    PayPalPayment payPalPayment = null;

                    if (payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER."))
                    {
                        var webhookPayPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();

                        // Fetch persisted order as it may have changed since the webhook 
                        // was initially sent (it could be a webhook resend)
                        var persistedPayPalOrder = await client.GetOrderAsync(webhookPayPalOrder.Id);

                        if (persistedPayPalOrder.Intent == PayPalOrder.Intents.AUTHORIZE)
                        {
                            // Authorize
                            payPalOrder = persistedPayPalOrder.Status != PayPalOrder.Statuses.APPROVED
                                ? persistedPayPalOrder
                                : await client.AuthorizeOrderAsync(persistedPayPalOrder.Id);

                            payPalPayment = payPalOrder.PurchaseUnits[0].Payments?.Authorizations?.FirstOrDefault();
                        }
                        else
                        {
                            // Capture
                            payPalOrder = persistedPayPalOrder.Status != PayPalOrder.Statuses.APPROVED
                                ? persistedPayPalOrder
                                : await client.CaptureOrderAsync(persistedPayPalOrder.Id);

                            payPalPayment = payPalOrder.PurchaseUnits[0].Payments?.Captures?.FirstOrDefault();
                        }

                        // Store the paypal order ID
                        metaData.Add("PayPalOrderId", payPalOrder.Id);
                    }
                    else if (payPalWebhookEvent.EventType.StartsWith("PAYMENT."))
                    {
                        // Listen for payment changes and update the status accordingly
                        // NB: These tend to be pretty delayed so shouldn't cause a huge issue but it's worth knowing
                        // that these will be notified after clicking the cancel / capture / refund buttons too so
                        // effectively the order will get updated twice. It's important to know as it could cause
                        // issues if they were to overlap and cause concurrency issues?
                        if (payPalWebhookEvent.ResourceType == PayPalWebhookEvent.ResourceTypes.Payment.AUTHORIZATION)
                        {
                            payPalPayment = payPalWebhookEvent.Resource.ToObject<PayPalAuthorizationPayment>();
                        }
                        else if (payPalWebhookEvent.ResourceType == PayPalWebhookEvent.ResourceTypes.Payment.CAPTURE)
                        {
                            payPalPayment = payPalWebhookEvent.Resource.ToObject<PayPalCapturePayment>();
                        }
                        else if (payPalWebhookEvent.ResourceType == PayPalWebhookEvent.ResourceTypes.Payment.REFUND)
                        {
                            payPalPayment = payPalWebhookEvent.Resource.ToObject<PayPalRefundPayment>();
                        }
                    }

                    return CallbackResult.Ok(new TransactionInfo
                    {
                        AmountAuthorized = decimal.Parse(payPalPayment?.Amount.Value ?? "0.00", CultureInfo.InvariantCulture),
                        TransactionId = payPalPayment?.Id ?? ctx.Order.TransactionInfo.TransactionId ?? "",
                        PaymentStatus = payPalOrder != null
                            ? GetPaymentStatus(payPalOrder)
                            : GetPaymentStatus(payPalPayment)
                    },
                    metaData);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - ProcessCallback");
            }

            return CallbackResult.BadRequest();
        }

        public override async Task<ApiResult> FetchPaymentStatusAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                if (ctx.Order.Properties.ContainsKey("PayPalOrderId"))
                {
                    var payPalOrderId = ctx.Order.Properties["PayPalOrderId"].Value;

                    var clientConfig = GetPayPalClientConfig(ctx.Settings);
                    var client = new PayPalClient(clientConfig);
                    var payPalOrder = await client.GetOrderAsync(payPalOrderId);

                    var paymentStatus = GetPaymentStatus(payPalOrder, out PayPalPayment payPalPayment);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = payPalPayment.Id,
                            PaymentStatus = paymentStatus
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CapturePaymentAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                if (ctx.Order.TransactionInfo.PaymentStatus == PaymentStatus.Authorized)
                {
                    var clientConfig = GetPayPalClientConfig(ctx.Settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = await client.CapturePaymentAsync(ctx.Order.TransactionInfo.TransactionId);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = payPalPayment.Id,
                            PaymentStatus = GetPaymentStatus(payPalPayment)
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> RefundPaymentAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                if (ctx.Order.TransactionInfo.PaymentStatus == PaymentStatus.Captured)
                {
                    var clientConfig = GetPayPalClientConfig(ctx.Settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = await client.RefundPaymentAsync(ctx.Order.TransactionInfo.TransactionId);

                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = payPalPayment.Id,
                            PaymentStatus = GetPaymentStatus(payPalPayment)
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - RefundPayment");
            }

            return ApiResult.Empty;
        }

        public override async Task<ApiResult> CancelPaymentAsync(PaymentProviderContext<PayPalCheckoutOneTimeSettings> ctx)
        {
            try
            {
                if (ctx.Order.TransactionInfo.PaymentStatus == PaymentStatus.Authorized)
                {
                    var clientConfig = GetPayPalClientConfig(ctx.Settings);
                    var client = new PayPalClient(clientConfig);

                    await client.CancelPayment(ctx.Order.TransactionInfo.TransactionId);

                    // Cancel payment enpoint doesn't return a result so if the request is successfull 
                    // then we'll deem it as successfull and directly set the payment status to Cancelled
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = ctx.Order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Cancelled
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PayPal - CancelPayment");
            }

            return ApiResult.Empty;
        }
    }
}
