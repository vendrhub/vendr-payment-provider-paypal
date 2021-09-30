using System.Linq;
using Vendr.Core.Models;
using Vendr.Core.Api;
using Vendr.Core.PaymentProviders;
using Vendr.PaymentProviders.PayPal.Api;
using Vendr.PaymentProviders.PayPal.Api.Models;
using System.Threading.Tasks;

namespace Vendr.PaymentProviders.PayPal
{
    public abstract class PayPalPaymentProviderBase<TSettings> : PaymentProviderBase<TSettings>
        where TSettings : PayPalSettingsBase, new()
    {
        public PayPalPaymentProviderBase(VendrContext vendr)
            : base(vendr)
        { }

        public override string GetCancelUrl(PaymentProviderContext<TSettings> ctx)
        {
            return ctx.Settings.CancelUrl;
        }

        public override string GetContinueUrl(PaymentProviderContext<TSettings> ctx)
        {
            return ctx.Settings.ContinueUrl;
        }

        public override string GetErrorUrl(PaymentProviderContext<TSettings> ctx)
        {
            return ctx.Settings.ErrorUrl;
        }

        protected async Task<PayPalWebhookEvent> GetPayPalWebhookEventAsync(PayPalClient client, PaymentProviderContext<TSettings> ctx)
        {
            PayPalWebhookEvent payPalWebhookEvent;

            if (ctx.AdditionalData.ContainsKey("Vendr_PayPalWebhookEvent"))
            {
                payPalWebhookEvent = (PayPalWebhookEvent)ctx.AdditionalData["Vendr_PayPalWebhookEvent"];
            }
            else
            {
                payPalWebhookEvent = await client.ParseWebhookEventAsync(ctx.Request);

                ctx.AdditionalData.Add("Vendr_PayPalWebhookEvent", payPalWebhookEvent);
            }

            return payPalWebhookEvent;
        }

        protected PaymentStatus GetPaymentStatus(PayPalOrder payPalOrder)
        {
            return GetPaymentStatus(payPalOrder, out PayPalPayment payPalPayment);
        }

        protected PaymentStatus GetPaymentStatus(PayPalOrder payPalOrder, out PayPalPayment payPalPayment)
        {
            payPalPayment = null;

            if (payPalOrder.PurchaseUnits != null && payPalOrder.PurchaseUnits.Length == 1)
            {
                var purchaseUnit = payPalOrder.PurchaseUnits[0];
                if (purchaseUnit.Payments != null)
                {
                    if (purchaseUnit.Payments.Refunds != null && purchaseUnit.Payments.Refunds.Length > 0)
                    {
                        payPalPayment = purchaseUnit.Payments.Refunds.First();
                    }
                    else if (purchaseUnit.Payments.Captures != null && purchaseUnit.Payments.Captures.Length > 0)
                    {
                        payPalPayment = purchaseUnit.Payments.Captures.First();
                    }
                    else if (purchaseUnit.Payments.Authorizations != null && purchaseUnit.Payments.Authorizations.Length > 0)
                    {
                        payPalPayment = purchaseUnit.Payments.Authorizations.First();
                    }

                    if (payPalPayment != null)
                    {
                        return GetPaymentStatus(payPalPayment);
                    }
                }
            }

            return PaymentStatus.Initialized;
        }

        protected PaymentStatus GetPaymentStatus(PayPalPayment payment)
        {
            if (payment is PayPalCapturePayment capturePayment)
            {
                switch (capturePayment.Status)
                {
                    case PayPalCapturePayment.Statuses.COMPLETED:
                        return PaymentStatus.Captured;
                    case PayPalCapturePayment.Statuses.PENDING:
                        return PaymentStatus.PendingExternalSystem;
                    case PayPalCapturePayment.Statuses.DECLINED:
                        return PaymentStatus.Error;
                    case PayPalCapturePayment.Statuses.REFUNDED:
                    case PayPalCapturePayment.Statuses.PARTIALLY_REFUNDED:
                        return PaymentStatus.Refunded;
                }
            }
            else if (payment is PayPalAuthorizationPayment authPayment)
            {
                switch (authPayment.Status)
                {
                    case PayPalAuthorizationPayment.Statuses.CREATED:
                        return PaymentStatus.Authorized;
                    case PayPalAuthorizationPayment.Statuses.PENDING:
                        return PaymentStatus.PendingExternalSystem;
                    case PayPalAuthorizationPayment.Statuses.CAPTURED:
                    case PayPalAuthorizationPayment.Statuses.PARTIALLY_CAPTURED:
                        return PaymentStatus.Captured;
                    case PayPalAuthorizationPayment.Statuses.DENIED:
                        return PaymentStatus.Error;
                    case PayPalAuthorizationPayment.Statuses.EXPIRED:
                    case PayPalAuthorizationPayment.Statuses.VOIDED:
                        return PaymentStatus.Cancelled;
                }
            }
            else if (payment is PayPalRefundPayment refundPayment)
            {
                switch (refundPayment.Status)
                {
                    case PayPalRefundPayment.Statuses.CANCELLED:
                    case PayPalRefundPayment.Statuses.PENDING:
                        return PaymentStatus.Captured;
                    case PayPalRefundPayment.Statuses.COMPLETED:
                        return PaymentStatus.Refunded;
                }
            }

            return PaymentStatus.Initialized;
        }

        protected PayPalClientConfig GetPayPalClientConfig(PayPalSettingsBase settings)
        {
            if (!settings.SandboxMode)
            {
                return new LivePayPalClientConfig
                {
                    ClientId = settings.LiveClientId,
                    Secret = settings.LiveSecret,
                    WebhookId = settings.LiveWebhookId
                };
            }
            else
            {
                return new SandboxPayPalClientConfig
                {
                    ClientId = settings.SandboxClientId,
                    Secret = settings.SandboxSecret,
                    WebhookId = settings.SandboxWebhookId
                };
            }
        }
    }
}
