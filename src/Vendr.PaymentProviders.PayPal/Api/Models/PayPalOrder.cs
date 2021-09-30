namespace Vendr.PaymentProviders.PayPal.Api.Models
{
    public class PayPalOrder : PayPalOrderBase<PayPalPurchaseUnit>
    {
        public static class Statuses
        {
            public const string CREATED = "CREATED";
            public const string SAVED = "SAVED";
            public const string APPROVED = "APPROVED";
            public const string VOIDED = "VOIDED";
            public const string COMPLETED = "COMPLETED";
        }

        public static class Intents
        {
            public const string CAPTURE = "CAPTURE";
            public const string AUTHORIZE = "AUTHORIZE";
        }
    }
}
