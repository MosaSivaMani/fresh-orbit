namespace FreshOrbit.Ordering.Domain.Invoices;

/// <summary>A state or union territory as printed in a GST place of supply: its name and two-digit GST state code.</summary>
public sealed record GstState(string Name, string Code);

/// <summary>
/// The statutory GST state code list (D344): maps the two-letter state code stored on an address (ISO 3166-2:IN, with
/// the common older vehicle-registration spellings) to the state name and the two-digit code that also opens a GSTIN.
/// This is national reference law, not store configuration; an address state outside it is refused, never guessed.
/// </summary>
public static class GstStates
{
    private static readonly Dictionary<string, GstState> ByAddressCode = new(StringComparer.Ordinal)
    {
        ["JK"] = new("Jammu and Kashmir", "01"),
        ["HP"] = new("Himachal Pradesh", "02"),
        ["PB"] = new("Punjab", "03"),
        ["CH"] = new("Chandigarh", "04"),
        ["UT"] = new("Uttarakhand", "05"),
        ["UK"] = new("Uttarakhand", "05"),
        ["HR"] = new("Haryana", "06"),
        ["DL"] = new("Delhi", "07"),
        ["RJ"] = new("Rajasthan", "08"),
        ["UP"] = new("Uttar Pradesh", "09"),
        ["BR"] = new("Bihar", "10"),
        ["SK"] = new("Sikkim", "11"),
        ["AR"] = new("Arunachal Pradesh", "12"),
        ["NL"] = new("Nagaland", "13"),
        ["MN"] = new("Manipur", "14"),
        ["MZ"] = new("Mizoram", "15"),
        ["TR"] = new("Tripura", "16"),
        ["ML"] = new("Meghalaya", "17"),
        ["AS"] = new("Assam", "18"),
        ["WB"] = new("West Bengal", "19"),
        ["JH"] = new("Jharkhand", "20"),
        ["OD"] = new("Odisha", "21"),
        ["OR"] = new("Odisha", "21"),
        ["CT"] = new("Chhattisgarh", "22"),
        ["CG"] = new("Chhattisgarh", "22"),
        ["MP"] = new("Madhya Pradesh", "23"),
        ["GJ"] = new("Gujarat", "24"),
        ["DH"] = new("Dadra and Nagar Haveli and Daman and Diu", "26"),
        ["DN"] = new("Dadra and Nagar Haveli and Daman and Diu", "26"),
        ["DD"] = new("Dadra and Nagar Haveli and Daman and Diu", "26"),
        ["MH"] = new("Maharashtra", "27"),
        ["KA"] = new("Karnataka", "29"),
        ["GA"] = new("Goa", "30"),
        ["LD"] = new("Lakshadweep", "31"),
        ["KL"] = new("Kerala", "32"),
        ["TN"] = new("Tamil Nadu", "33"),
        ["PY"] = new("Puducherry", "34"),
        ["AN"] = new("Andaman and Nicobar Islands", "35"),
        ["TG"] = new("Telangana", "36"),
        ["TS"] = new("Telangana", "36"),
        ["AP"] = new("Andhra Pradesh", "37"),
        ["LA"] = new("Ladakh", "38"),
    };

    /// <summary>The state for an address's two-letter code, or null when the code is not an Indian state or UT.</summary>
    public static GstState? Find(string? addressStateCode)
        => addressStateCode is not null && ByAddressCode.TryGetValue(addressStateCode.Trim().ToUpperInvariant(), out var state) ? state : null;

    /// <summary>The two-digit state code a GSTIN starts with, or null when the GSTIN is too short.</summary>
    public static string? CodeOfGstin(string? gstin)
        => gstin is { Length: >= 2 } ? gstin[..2] : null;
}
