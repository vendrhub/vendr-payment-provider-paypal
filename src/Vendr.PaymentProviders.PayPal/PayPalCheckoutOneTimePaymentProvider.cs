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

                if (client.IsIpnRequest(request))
                {
                    // Try processing an IPN
                    // NB: At this stage we just need to get the order reference
                    // we'll let the ProcessCallback handle actually verifying 
                    // the IPN is entirely valid
                    if (!string.IsNullOrWhiteSpace(request.Form["custom"]))
                    {
                        return OrderReference.Parse(request.Form["custom"]);
                    }
                    else if (!string.IsNullOrWhiteSpace(request.Form["custom_id"]))
                    {
                        return OrderReference.Parse(request.Form["custom_id"]);
                    }
                }
                else
                {
                    // If it's not an IPN then it must be a webhook
                    var payPalWebhookEvent = GetPayPalWebhookEvent(client, request);

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
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - GetOrderReference");
            }

            return base.GetOrderReference(request, settings);
        }

        public override PaymentFormResult GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, PayPalCheckoutOneTimeSettings settings)
        {
            // Get currency information
            var currency = Vendr.Services.CurrencyService.GetCurrency(order.CurrencyId);
            var currencyCode = currency.Code.ToUpperInvariant();

            // Ensure currency has valid ISO 4217 code
            if (!Iso4217.CurrencyCodes.ContainsKey(currencyCode))
            {
                throw new Exception("Currency must be a valid ISO 4217 currency code: " + currency.Name);
            }

            // Create the order
            var clientConfig = GetPayPalClientConfig(settings);
            var client = new PayPalClient(clientConfig);
            var payPalOrder = client.CreateOrder(new PayPalCreateOrderRequest
            {
                Intent = settings.Capture 
                    ? PayPalOrder.Intents.CAPTURE 
                    : PayPalOrder.Intents.AUTHORIZE,
                PurchaseUnits = new[] 
                {
                    new PayPalPurchaseUnitRequest
                    {
                        CustomId = order.GenerateOrderReference(),
                        Amount = new PayPalAmount
                        {
                            CurrencyCode = currencyCode,
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

            return new PaymentFormResult()
            {
                Form = new PaymentForm(approveLink.Href, approveLinkMethod)
            };
        }

        public override CallbackResult ProcessCallback(OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                var clientConfig = GetPayPalClientConfig(settings);
                var client = new PayPalClient(clientConfig);

                if (client.IsIpnRequest(request))
                {
                    return ProcessIpnCallback(client, order, request, settings);
                }
                else
                {
                    return ProcessWebhookCallback(client, order, request, settings);
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - ProcessCallback");
            }

            return CallbackResult.BadRequest();
        }

        public CallbackResult ProcessIpnCallback(PayPalClient client, OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            if (client.VerifyIpnRequest(request))
            {
                var receiverId = request.Form["receiver_id"];
                var receiverEmail = request.Form["receiver_email"];
                var transactionId = request.Form["txn_id"];
                var paymentState = request.Form["payment_status"];
                var amount = decimal.Parse(request.Form["mc_gross"], CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(transactionId)
                    && ((!string.IsNullOrWhiteSpace(receiverId) && receiverId == settings.Recipient) || (!string.IsNullOrWhiteSpace(receiverEmail) && receiverEmail == settings.Recipient)))
                {
                    var paymentStatus = PaymentStatus.Initialized;

                    if (paymentState == "Pending")
                    {
                        if (request.Form["pending_reason"] == "authorization")
                        {
                            if (request.Form["transaction_entity"] == "auth")
                            {
                                paymentStatus = PaymentStatus.Authorized;
                            }
                        }
                        else if (request.Form["pending_reason"] == "multi_currency")
                        {
                            paymentStatus = PaymentStatus.PendingExternalSystem;
                        }
                    }
                    else if (paymentState == "Completed")
                    {
                        paymentStatus = PaymentStatus.Captured;
                    }

                    return CallbackResult.Ok(new TransactionInfo
                    {
                        AmountAuthorized = amount,
                        TransactionId = transactionId,
                        PaymentStatus = paymentStatus
                    });
                }
            }

            return CallbackResult.Ok();
        }

        public CallbackResult ProcessWebhookCallback(PayPalClient client, OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            var payPalWebhookEvent = GetPayPalWebhookEvent(client, request);

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
                    var persistedPayPalOrder = client.GetOrder(webhookPayPalOrder.Id);

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
                    AmountAuthorized = decimal.Parse(payPalPayment?.Amount.Value ?? "0.00"),
                    TransactionId = payPalPayment?.Id ?? order.TransactionInfo.TransactionId ?? "",
                    PaymentStatus = payPalOrder != null
                        ? GetPaymentStatus(payPalOrder)
                        : GetPaymentStatus(payPalPayment)
                },
                metaData);
            }

            return CallbackResult.BadRequest();
        }

        public override ApiResult FetchPaymentStatus(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
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
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - FetchPaymentStatus");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CapturePayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                if (order.TransactionInfo.PaymentStatus == PaymentStatus.Authorized)
                {
                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = client.CapturePayment(order.TransactionInfo.TransactionId);

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
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - CapturePayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult RefundPayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                if (order.TransactionInfo.PaymentStatus == PaymentStatus.Captured)
                {
                    var clientConfig = GetPayPalClientConfig(settings);
                    var client = new PayPalClient(clientConfig);

                    var payPalPayment = client.RefundPayment(order.TransactionInfo.TransactionId);

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
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - RefundPayment");
            }

            return ApiResult.Empty;
        }

        public override ApiResult CancelPayment(OrderReadOnly order, PayPalCheckoutOneTimeSettings settings)
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
                    return new ApiResult()
                    {
                        TransactionInfo = new TransactionInfoUpdate()
                        {
                            TransactionId = order.TransactionInfo.TransactionId,
                            PaymentStatus = PaymentStatus.Cancelled
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - CancelPayment");
            }

            return ApiResult.Empty;
        }
    }
}
