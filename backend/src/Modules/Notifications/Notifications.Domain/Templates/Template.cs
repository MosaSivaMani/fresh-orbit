using System.Text;
using System.Text.RegularExpressions;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Notifications.Domain.Templates;

/// <summary>
/// One channel of a template code. <see cref="Variables"/> is the fixed list the code may use (NOT-R1); common
/// variables are always allowed. <see cref="Category"/> is null for templates that ignore preferences (OTP, staff).
/// </summary>
public sealed class Template : AggregateRoot
{
    /// <summary>Available to every template (NOT-R1).</summary>
    public static readonly IReadOnlyList<string> CommonVariables = ["businessName", "supportPhone", "supportEmail", "recipientName"];

    private Template()
    {
        Code = Body = string.Empty;
        Variables = [];
    }

    public string Code { get; private set; }

    public Channel Channel { get; private set; }

    public Category? Category { get; private set; }

    /// <summary>EMAIL subject or PUSH title; null for SMS and IN_APP (IN_APP uses it as title when present).</summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; }

    public bool IsActive { get; private set; }

    public string[] Variables { get; private set; }

    /// <summary>The operator DLT template id of an SMS template (required by real SMS providers in India).</summary>
    public string? DltTemplateId { get; private set; }

    public static Template Create(string code, Channel channel, Category? category, string? subject, string body, IReadOnlyCollection<string> variables, string? dltTemplateId)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var template = new Template { Code = code, Channel = channel, Category = category, Variables = [.. variables], DltTemplateId = dltTemplateId };
        template.Edit(subject, body, true);
        return template;
    }

    /// <summary>Reference data refresh: definition fields only (texts edited by staff are kept).</summary>
    public void Redefine(Category? category, IReadOnlyCollection<string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        Category = category;
        Variables = [.. variables];
    }

    public void Edit(string? subject, string body, bool isActive)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 4000)
        {
            errors.Add(new FieldError("body", "VALUE_INVALID", "The body must be 1 to 4000 characters."));
        }

        if (Channel is Channel.Email or Channel.Push && string.IsNullOrWhiteSpace(subject))
        {
            errors.Add(new FieldError("subject", "VALUE_REQUIRED", "EMAIL and PUSH templates need a subject (title)."));
        }

        if (subject is { Length: > 200 })
        {
            errors.Add(new FieldError("subject", "VALUE_INVALID", "The subject must be at most 200 characters."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }

        var unknown = TemplateText.Placeholders(subject).Concat(TemplateText.Placeholders(body))
            .Where(p => !Variables.Contains(p, StringComparer.Ordinal) && !CommonVariables.Contains(p, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count > 0)
        {
            throw new DomainException(
                NotificationsErrors.TemplatePlaceholderUnknown,
                $"Unknown placeholder(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", Variables.Concat(CommonVariables))}.",
                "body");
        }

        Subject = string.IsNullOrWhiteSpace(subject) ? null : subject;
        Body = body;
        IsActive = isActive;
    }
}

/// <summary><c>{{name}}</c> placeholders (NOT-R1, NOT-R5).</summary>
public static partial class TemplateText
{
    public static IEnumerable<string> Placeholders(string? text)
        => text is null ? [] : PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value);

    /// <summary>Replaces each placeholder with its value; missing values render as empty text.</summary>
    public static string Render(string text, IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(data);
        return PlaceholderPattern().Replace(text, m => data.TryGetValue(m.Groups[1].Value, out var value) ? value : string.Empty);
    }

    /// <summary>Sample data for a test send: each variable renders as <c>[name]</c>.</summary>
    public static IReadOnlyDictionary<string, string> SampleData(IEnumerable<string> variables)
    {
        var sample = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            sample[variable] = new StringBuilder().Append('[').Append(variable).Append(']').ToString();
        }

        return sample;
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}
