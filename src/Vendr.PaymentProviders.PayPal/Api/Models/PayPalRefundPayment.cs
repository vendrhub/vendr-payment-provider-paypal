namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalRefundPayment : PayPalPayment
    {
        public static class Statuses
        {
            public const string CANCELLED = "CANCELLED";
            public const string PENDING = "PENDING";
            public const string COMPLETED = "COMPLETED";
        }
    }
}
