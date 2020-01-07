using System;
using System.Linq;
using System.Web;
using Flurl.Http;
using Vendr.Core;
using Vendr.Core.Web;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.PaymentProviders.PayPal.Api.Models;
using System.Web.Mvc;
using Vendr.PaymentProviders.PayPal.Api;

namespace Vendr.PaymentProviders.PayPal
{
    [PaymentProvider("paypal-checkout-onetime", "PayPal Checkout (One Time)", "PayPal Checkout payment provider for one time payments")]
    public class PayPalCheckoutOneTimePaymentProvider : PayPalPaymentProviderBase<PayPalCheckoutOneTimeSettings>
    {
        public PayPalCheckoutOneTimePaymentProvider(VendrContext vendr)
            : base(vendr)
        { }

        public override bool CanFetchPaymentStatus => true;
        public override bool CanCapturePayments => true;
        public override bool CanCancelPayments => true;
        public override bool CanRefundPayments => true;

        // Don't finalize at continue as we will finalize async via webhook
        public override bool FinalizeAtContinueUrl => false;

        public override OrderReference GetOrderReference(HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(settings);
                var client = new PayPalClient(clientConfig);

                //var payPalWebhookEvent = GetPayPalWebhookEvent(requestConfig, request);
                
                //if (payPalWebhookEvent != null && payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER."))
                //{
                //    var payPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();

                //    return OrderReference.Parse(payPalOrder.PurchaseUnits[0].CustomId);
                //}
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - GetOrderReference");
            }

            return base.GetOrderReference(request, settings);
        }

        public override PaymentForm GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, PayPalCheckoutOneTimeSettings settings)
        {
            // Create the order
            var clientConfig = GetPayPalClientConfig(settings);
            var client = new PayPalClient(clientConfig);
            var createOrderResponse = client.CreateOrder(new PayPalCreateOrderRequest
            {
                Intent = PayPalOrderIntent.CAPTURE,
                PurchaseUnits = new[] {
                    new PayPalPurchaseUnitRequest {
                        CustomId = order.GenerateOrderReference(),
                        Amount = new PayPalAmount{
                            CurrencyCode = "USD",
                            Value = 0m
                        }
                    }
                },
                AplicationContext = new PayPalOrderApplicationContext
                {
                    BrandName = "Test",
                    UserAction = "PAY_NOW",
                    ReturnUrl = continueUrl,
                    CancelUrl = cancelUrl
                }
            });

            // Setup the payment form to redirect to approval link
            var approveLink = createOrderResponse.Links.FirstOrDefault(x => x.Rel == "approve");
            var approveLinkMethod = (FormMethod)Enum.Parse(typeof(FormMethod), approveLink.Method);

            return new PaymentForm(approveLink.Href, approveLinkMethod);
        }

        public override CallbackResponse ProcessCallback(OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                var webhookId = settings.LiveWebhookId;

                var clientConfig = GetPayPalClientConfig(settings);
                var client = new PayPalClient(clientConfig);
                var payPalWebhookEvent = client.ParseWebhookEvent(request, webhookId);

                if (payPalWebhookEvent != null && payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER."))
                {
                    var payPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - ProcessCallback");
            }

            return CallbackResponse.Empty;
        }

        public override ApiResponse FetchPaymentStatus(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - FetchPaymentStatus");
            }

            return null;
        }

        public override ApiResponse CapturePayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - CapturePayment");
            }

            return null;
        }

        public override ApiResponse RefundPayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - RefundPayment");
            }

            return null;
        }

        public override ApiResponse CancelPayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - CancelPayment");
            }

            return null;
        }
    }
}
