namespace FreshOrbit.Payments.Domain.Shared;

/// <summary>Stored provider codes (<c>payments.provider</c>); each is the <c>Key</c> of an <c>IPaymentProvider</c> adapter.</summary>
public static class PaymentProviders
{
    /// <summary>Cash on delivery: collected by the rider.</summary>
    public const string Cash = "CASH";

    /// <summary>UPI on delivery: rider QR, reference typed by staff.</summary>
    public const string UpiOnDelivery = "UPI_ON_DELIVERY";

    /// <summary>Scriptable fake gateway for development and tests (refused in Production).</summary>
    public const string Fake = "FAKE";

    public const string Razorpay = "RAZORPAY";

    /// <summary>The <c>INR</c> currency, the only one accepted.</summary>
    public const string Inr = "INR";
}
