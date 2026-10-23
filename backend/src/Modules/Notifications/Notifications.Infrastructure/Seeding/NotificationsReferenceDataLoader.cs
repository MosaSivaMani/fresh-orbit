using System.Text.Json;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Domain.Templates;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Notifications.Infrastructure.Seeding;

/// <summary>
/// Loads <c>notifications/notification-templates.json</c>: missing (code, channel) rows are inserted; for existing rows
/// only the definition (category, variables) is refreshed, so texts edited by staff are kept.
/// </summary>
public sealed partial class NotificationsReferenceDataLoader(FreshOrbitDbContext db, ILogger<NotificationsReferenceDataLoader> logger) : IReferenceDataLoader
{
    public int Order => 40;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = Path.Combine(context.DirectoryPath, "notifications", "notification-templates.json");
        TemplatesFile file;
        await using (var stream = File.OpenRead(path))
        {
            file = await JsonSerializer.DeserializeAsync<TemplatesFile>(stream, FreshOrbitJson.Options, cancellationToken)
                ?? throw new InvalidDataException($"{path} is empty.");
        }

        if (file.Templates is null || file.Templates.Count == 0)
        {
            throw new InvalidDataException($"{path}: 'templates' must not be empty.");
        }

        // Validate every row (placeholders included) before any write.
        var rows = file.Templates.Select(r => Template.Create(
            r.Code ?? throw new InvalidDataException($"{path}: a template has no code."),
            EnumCodes.ToEnum<Channel>(r.Channel ?? string.Empty),
            r.Category is null ? null : EnumCodes.ToEnum<Category>(r.Category),
            r.Subject,
            r.Body ?? string.Empty,
            r.Variables ?? [],
            r.DltTemplateId)).ToList();
        var existing = await db.Set<Template>().ToListAsync(cancellationToken);
        int inserted = 0, updated = 0;
        foreach (var row in rows)
        {
            var current = existing.FirstOrDefault(t => t.Code == row.Code && t.Channel == row.Channel);
            if (current is null)
            {
                db.Add(row);
                inserted++;
            }
            else if (current.Category != row.Category || !current.Variables.SequenceEqual(row.Variables, StringComparer.Ordinal))
            {
                current.Redefine(row.Category, row.Variables);
                updated++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(inserted, updated, rows.Count - inserted - updated);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data notification-templates.json: {Inserted} inserted, {Updated} updated, {Unchanged} unchanged")]
    private partial void LogLoaded(int inserted, int updated, int unchanged);

    private sealed record TemplatesFile(int Version, List<TemplateRow>? Templates);

    private sealed record TemplateRow(string? Code, string? Channel, string? Category, List<string>? Variables, string? Subject, string? Body, string? DltTemplateId);
}
