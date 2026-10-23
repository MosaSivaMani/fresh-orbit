namespace FreshOrbit.Settings.Domain.Shared;

/// <summary>Error codes of the Settings module (docs/api §2).</summary>
public static class SettingsErrors
{
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string GstinInvalid = "GSTIN_INVALID";
    public const string NumberSeriesInUse = "NUMBER_SERIES_IN_USE";
    public const string Duplicate = "DUPLICATE";
    public const string ValidationFailed = "VALIDATION_FAILED";
}
