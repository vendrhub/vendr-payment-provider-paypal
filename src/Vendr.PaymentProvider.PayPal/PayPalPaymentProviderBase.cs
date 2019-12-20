using Vendr.Core.Models;
using Vendr.Core.Web.Api;
using Vendr.Core.Web.PaymentProviders;

namespace Vendr.PaymentProvider.PayPal
{
    public abstract class PayPalPaymentProviderBase<TSettings> : PaymentProviderBase<TSettings>
        where TSettings : PayPalSettingsBase, new()
    {
        public PayPalPaymentProviderBase(VendrContext vendr)
            : base(vendr)
        { }

        public override string GetCancelUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.CancelUrl;
        }

        public override string GetContinueUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.ContinueUrl;
        }

        public override string GetErrorUrl(OrderReadOnly order, TSettings settings)
        {
            return settings.ErrorUrl;
        }

        //protected static long DollarsToCents(decimal val)
        //{
        //    var cents = val * 100M;
        //    var centsRounded = Math.Round(cents, MidpointRounding.AwayFromZero);
        //    return Convert.ToInt64(centsRounded);
        //}

        //protected static decimal CentsToDollars(long val)
        //{
        //    return val / 100M;
        //}
    }
}
