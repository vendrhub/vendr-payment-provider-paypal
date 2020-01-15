using System;
using System.Linq;
using System.Web;
using Vendr.Core;
using Vendr.Core.Web;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;
using Vendr.PaymentProviders.PayPal.Api.Models;
using System.Web.Mvc;
using Vendr.PaymentProviders.PayPal.Api;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;

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

        public override IEnumerable<TransactionMetaDataDefinition> TransactionMetaDataDefinitions => new []{
            new TransactionMetaDataDefinition("PayPalOrderId", "PayPal Order ID")
        };

        public override OrderReference GetOrderReference(HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(settings);
                var client = new PayPalClient(clientConfig);
                var payPalWebhookEvent = GetPayPalWebhookEvent(client, request);

                if (payPalWebhookEvent != null && payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER."))
                {
                    var payPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();
                    if (payPalOrder?.PurchaseUnits != null && payPalOrder.PurchaseUnits.Length == 1)
                    {
                        return OrderReference.Parse(payPalOrder.PurchaseUnits[0].CustomId);
                    }
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - GetOrderReference");
            }

            return base.GetOrderReference(request, settings);
        }

        public override PaymentForm GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, PayPalCheckoutOneTimeSettings settings)
        {
            // Get currency information
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);

            // Create the order
            var clientConfig = GetPayPalClientConfig(settings);
            var client = new PayPalClient(clientConfig);
            var payPalOrder = client.CreateOrder(new PayPalCreateOrderRequest
            {
                Intent = settings.Capture 
                    ? PayPalOrder.Intents.CAPTURE 
                    : PayPalOrder.Intents.AUTHORIZE,
                PurchaseUnits = new[] {
                    new PayPalPurchaseUnitRequest {
                        CustomId = order.GenerateOrderReference(),
                        Amount = new PayPalAmount{
                            CurrencyCode = currency.Code,
                            Value = order.TotalPrice.Value.WithTax.ToString("0.00", CultureInfo.InvariantCulture)
                        }
                    }
                },
                AplicationContext = new PayPalOrderApplicationContext
                {
                    BrandName = settings.BrandName,
                    UserAction = "PAY_NOW",
                    ReturnUrl = continueUrl,
                    CancelUrl = cancelUrl
                }
            });

            // Setup the payment form to redirect to approval link
            var approveLink = payPalOrder.Links.FirstOrDefault(x => x.Rel == "approve");
            var approveLinkMethod = (FormMethod)Enum.Parse(typeof(FormMethod), approveLink.Method, true);

            return new PaymentForm(approveLink.Href, approveLinkMethod);
        }

        public override CallbackResponse ProcessCallback(OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(settings);
                var client = new PayPalClient(clientConfig);
                var payPalWebhookEvent = GetPayPalWebhookEvent(client, request);

                if (payPalWebhookEvent != null && payPalWebhookEvent.EventType.StartsWith("CHECKOUT.ORDER.APPROVED"))
                {
                    var webhookPayPalOrder = payPalWebhookEvent.Resource.ToObject<PayPalOrder>();

                    // Fetch persisted order as it may have changed since the webhook 
                    // was initially sent (it could be a webhook resend)
                    var persistedPayPalOrder = client.GetOrder(webhookPayPalOrder.Id);

                    PayPalOrder payPalOrder;
                    PayPalPayment payPalPayment;

                    if (persistedPayPalOrder.Intent == PayPalOrder.Intents.AUTHORIZE)
                    {
                        // Authorize
                        payPalOrder = persistedPayPalOrder.Status != PayPalOrder.Statuses.APPROVED
                            ? persistedPayPalOrder
                            : client.AuthorizeOrder(persistedPayPalOrder.Id);

                        payPalPayment = payPalOrder.PurchaseUnits[0].Payments?.Authorizations?.FirstOrDefault();
                    }
                    else
                    {
                        // Capture
                        payPalOrder = persistedPayPalOrder.Status != PayPalOrder.Statuses.APPROVED
                            ? persistedPayPalOrder
                            : client.CaptureOrder(persistedPayPalOrder.Id);

                        payPalPayment = payPalOrder.PurchaseUnits[0].Payments?.Captures?.FirstOrDefault();
                    }

                    return new CallbackResponse
                    {
                        TransactionInfo = new TransactionInfo
                        {
                            AmountAuthorized = decimal.Parse(payPalPayment?.Amount.Value ?? "0.00"),
                            TransactionId = payPalPayment?.Id ?? "",
                            PaymentStatus = GetPaymentStatus(payPalOrder)
                        },
                        MetaData = new Dictionary<string, string>
                        {
                            { "PayPalOrderId", payPalOrder.Id }
                        },
                        HttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - ProcessCallback");
            }

            return new CallbackResponse
            {
                HttpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            };
        }

        public override ApiResponse FetchPaymentStatus(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                if (order.Properties.ContainsKey("PayPalOrderId"))
                {
                    var payPalOrderId = order.Properties["PayPalOrderId"].Value;

                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);
                    var payPalOrder = client.GetOrder(payPalOrderId);

                    var paymentStatus = GetPaymentStatus(payPalOrder, out PayPalPayment payPalPayment);

                    return new ApiResponse(payPalPayment.Id, paymentStatus);
                }
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
                if (order.TransactionInfo.PaymentStatus == PaymentStatus.Authorized)
                {
                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = client.CapturePayment(order.TransactionInfo.TransactionId);

                    return new ApiResponse(payPalPayment.Id, GetPaymentStatus(payPalPayment));
                }
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
                if (order.TransactionInfo.PaymentStatus == PaymentStatus.Captured)
                {
                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = client.RefundPayment(order.TransactionInfo.TransactionId);

                    return new ApiResponse(payPalPayment.Id, GetPaymentStatus(payPalPayment));
                }
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
                if (order.TransactionInfo.PaymentStatus == PaymentStatus.Authorized)
                {
                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);

                    client.CancelPayment(order.TransactionInfo.TransactionId);

                    // Cancel payment enpoint doesn't return a result so if the request is successfull 
                    // then we'll deem it as successfull and directly set the payment status to Cancelled
                    return new ApiResponse(order.TransactionInfo.TransactionId, PaymentStatus.Cancelled);
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - CancelPayment");
            }

            return null;
        }
    }
}
