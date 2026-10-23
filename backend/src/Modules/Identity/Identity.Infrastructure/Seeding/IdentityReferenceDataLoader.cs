using System.Text.Json;
using System.Text.RegularExpressions;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Shared;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Identity.Infrastructure.Seeding;

/// <summary>
/// IDN-R18: <c>identity/permissions.json</c> is synced (upsert; a database code missing from the file fails the load)
/// and <c>identity/roles.json</c> inserts missing system roles; ADMIN always receives every permission. <c>"*"</c> in
/// a role's list means every permission of the file.
/// </summary>
public sealed partial class IdentityReferenceDataLoader(FreshOrbitDbContext db, ILogger<IdentityReferenceDataLoader> logger) : IReferenceDataLoader
{
    public int Order => 10;

    public async Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var directory = Path.Combine(context.DirectoryPath, "identity");
        var codes = await LoadPermissionsAsync(Path.Combine(directory, "permissions.json"), cancellationToken);
        await LoadRolesAsync(Path.Combine(directory, "roles.json"), codes, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> LoadPermissionsAsync(string path, CancellationToken cancellationToken)
    {
        var file = await ReadAsync<PermissionsFile>(path, cancellationToken);
        var rows = file.Permissions ?? [];
        if (rows.Count == 0 || rows.Any(p => p.Code is null || !PermissionCode().IsMatch(p.Code) || string.IsNullOrWhiteSpace(p.Description)))
        {
            throw new InvalidDataException($"{path}: every permission needs a dotted lowercase code and a description.");
        }

        if (rows.GroupBy(p => p.Code).Any(g => g.Count() > 1))
        {
            throw new InvalidDataException($"{path}: permission codes must be unique.");
        }

        var existing = await db.Set<Permission>().ToDictionaryAsync(p => p.Id, cancellationToken);
        var removed = existing.Keys.Except(rows.Select(r => r.Code!)).ToList();
        if (removed.Count > 0)
        {
            throw new InvalidDataException($"{path}: permissions {string.Join(", ", removed)} exist in the database but not in the file; removing a permission needs a migration.");
        }

        int inserted = 0, updated = 0;
        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.Code!, out var permission))
            {
                if (permission.Description != row.Description)
                {
                    permission.Describe(row.Description!);
                    updated++;
                }
            }
            else
            {
                db.Add(new Permission(row.Code!, row.Description!));
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded("permissions.json", inserted, updated, rows.Count - inserted - updated);
        return [.. rows.Select(r => r.Code!)];
    }

    private async Task LoadRolesAsync(string path, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken)
    {
        var file = await ReadAsync<RolesFile>(path, cancellationToken);
        var rows = file.Roles ?? [];
        var known = permissionCodes.ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Code is null || !ErrorCodes.IsUpperSnake(row.Code) || string.IsNullOrWhiteSpace(row.Name) || row.Permissions is null)
            {
                throw new InvalidDataException($"{path}: every role needs an UPPER_SNAKE code, a name and a permissions list.");
            }

            var unknown = row.Permissions.Where(p => p != "*" && !known.Contains(p)).ToList();
            if (unknown.Count > 0)
            {
                throw new InvalidDataException($"{path}: role {row.Code} lists unknown permissions {string.Join(", ", unknown)}.");
            }
        }

        if (rows.All(r => r.Code != IdentityErrors.AdminRoleCode))
        {
            throw new InvalidDataException($"{path}: the ADMIN role is required.");
        }

        var existing = await db.Set<Role>().ToDictionaryAsync(r => r.Code, cancellationToken);
        int inserted = 0, updated = 0;
        foreach (var row in rows)
        {
            var permissions = row.Permissions!.Contains("*") ? permissionCodes : row.Permissions!;
            if (!existing.TryGetValue(row.Code!, out var role))
            {
                role = Role.Create(row.Code!, row.Name!, true, permissions);
                db.Add(role);
                existing[role.Code] = role;
                inserted++;
            }
            else if (role.IsAdmin)
            {
                var before = role.Permissions.Count;
                role.GrantMissing(permissionCodes);
                updated += role.Permissions.Count > before ? 1 : 0;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded("roles.json", inserted, updated, rows.Count - inserted - updated);
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, FreshOrbitJson.Options, cancellationToken)
            ?? throw new InvalidDataException($"{path} is empty.");
    }

    [GeneratedRegex("^[a-z]+(\\.[a-z]+)+$")]
    private static partial Regex PermissionCode();

    [LoggerMessage(Level = LogLevel.Information, Message = "Reference data {File}: {Inserted} inserted, {Updated} updated, {Skipped} unchanged")]
    private partial void LogLoaded(string file, int inserted, int updated, int skipped);

    private sealed record PermissionsFile(int Version, List<PermissionRow>? Permissions);

    private sealed record PermissionRow(string? Code, string? Description);

    private sealed record RolesFile(int Version, List<RoleRow>? Roles);

    private sealed record RoleRow(string? Code, string? Name, List<string>? Permissions);
}
