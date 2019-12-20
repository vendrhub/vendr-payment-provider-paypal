using System;
using System.Web;
using Vendr.Core;
using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProvider.PayPal
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
                
            }
            catch (Exception ex)
            {
                Vendr.Log.Error<PayPalCheckoutOneTimePaymentProvider>(ex, "PayPal - GetOrderReference");
            }

            return base.GetOrderReference(request, settings);
        }

        public override PaymentForm GenerateForm(OrderReadOnly order, string continueUrl, string cancelUrl, string callbackUrl, PayPalCheckoutOneTimeSettings settings)
        {
            return null;
        }

        public override CallbackResponse ProcessCallback(OrderReadOnly order, HttpRequestBase request, PayPalCheckoutOneTimeSettings settings)
        {
            try
            {
                
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
