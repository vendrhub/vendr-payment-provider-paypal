namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalAuthorizationPayment : PayPalPayment
    {
        public static class Statuses
        {
            public const string CREATED = "CREATED";
            public const string CAPTURED = "CAPTURED";
            public const string DENIED = "DENIED";
            public const string EXPIRED = "EXPIRED";
            public const string PARTIALLY_CAPTURED = "PARTIALLY_CAPTURED";
            public const string VOIDED = "VOIDED";
            public const string PENDING = "PENDING";

        }
    }
}
