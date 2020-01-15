namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalCapturePayment : PayPalPayment
    {
        public static class Statuses
        {
            public const string COMPLETED = "COMPLETED";
            public const string DECLINED = "DECLINED";
            public const string PARTIALLY_REFUNDED = "PARTIALLY_REFUNDED";
            public const string PENDING = "PENDING";
            public const string REFUNDED = "REFUNDED";
        }
    }
}
