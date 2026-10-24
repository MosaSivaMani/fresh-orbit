using System.Security.Cryptography;
using FluentValidation;
using FreshOrbit.Identity.Application.Shared;
using FreshOrbit.Identity.Application.Staff;
using FreshOrbit.Identity.Domain.Roles;
using FreshOrbit.Identity.Domain.Staff;
using FreshOrbit.Identity.Domain.Users;
using FreshOrbit.Identity.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FreshOrbit.Identity.Infrastructure.Seeding;

/// <summary>
/// Sample <c>identity.json</c> and the customer phones of <c>customers.json</c> (order 20).
/// Staff go through <see cref="IStaffService"/> with the password from <c>FRESHORBIT_SAMPLE_STAFF_PASSWORD</c>, or a
/// generated one printed once to the console (D57). Customer users are created as the first OTP verify creates them:
/// user ACTIVE with the phone verified at T0, and the customer row from <see cref="ICustomerAccountDirectory"/>.
/// Existing emails and phones are skipped.
/// </summary>
public sealed partial class IdentitySampleDataLoader(
    IStaffService staff,
    IValidator<CreateStaffRequest> validator,
    ICustomerAccountDirectory customers,
    FreshOrbitDbContext db,
    ILogger<IdentitySampleDataLoader> logger) : ISampleDataLoader
{
    public const string PasswordVariable = "FRESHORBIT_SAMPLE_STAFF_PASSWORD";

    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public int Order => 20;

    public async Task LoadAsync(SampleDataContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var file = await SampleFiles.ReadAsync<IdentityFile>(context, "identity.json", cancellationToken);
        var customerFile = await SampleFiles.ReadAsync<CustomersFile>(context, "customers.json", cancellationToken);

        // Staff
        var emails = file.Staff.Select(s => s.Email.Trim().ToLowerInvariant()).ToList();
        var existingEmails = (await db.Set<User>().AsNoTracking().Where(u => u.Email != null && emails.Contains(u.Email.ToLower())).Select(u => u.Email!.ToLower()).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var roleIds = await db.Set<Role>().AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);
        var toCreate = file.Staff.Where(s => !existingEmails.Contains(s.Email.Trim().ToLowerInvariant())).ToList();
        if (toCreate.Count > 0)
        {
            var password = Environment.GetEnvironmentVariable(PasswordVariable);
            var generated = string.IsNullOrEmpty(password);
            if (generated)
            {
                password = RandomNumberGenerator.GetString(PasswordAlphabet, 20);
            }

            foreach (var row in toCreate)
            {
                if (row.LocationCodes.Count > 0)
                {
                    throw new InvalidDataException($"identity.json: staff {row.Key} lists locationCodes; sample staff must use allLocations (Identity does not read Locations).");
                }

                var ids = row.RoleCodes.Select(code => roleIds.TryGetValue(code, out var id) ? id : throw SampleFiles.Missing("identity.json", "role", code)).ToList();
                var request = new CreateStaffRequest(row.Email, row.FullName, ids, row.AllLocations, [], password!);
                await validator.ValidateAndThrowAsync(request, cancellationToken);
                var created = await staff.CreateAsync(request, cancellationToken);
                context.Track<User>(db, created.UserId);
                context.Track<StaffMember>(db, created.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            if (generated)
            {
                // D57: printed once, never written to a file or the log.
                await Console.Out.WriteLineAsync($"Sample staff password (all sample staff, shown once; set {PasswordVariable} to choose it): {password}");
            }
        }

        // Customer users (first OTP verify path)
        var phones = customerFile.Customers.Select(c => c.Phone).ToList();
        var existingPhones = (await db.Set<User>().AsNoTracking().Where(u => u.Phone != null && phones.Contains(u.Phone)).Select(u => u.Phone!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var insertedCustomers = 0;
        foreach (var row in customerFile.Customers.Where(c => !existingPhones.Contains(c.Phone)))
        {
            if (!PhoneNumbers.IsE164(row.Phone))
            {
                throw new InvalidDataException($"customers.json: customer {row.Key} has a phone that is not E.164.");
            }

            var user = User.RegisterCustomer(row.Phone, context.StartedAt);
            db.Add(user);
            var account = await customers.EnsureCustomerAsync(user.Id, cancellationToken);
            context.Track<User>(db, user.Id);
            context.Tracker.Track(SampleTables.Customers, account.CustomerId);
            insertedCustomers++;
        }

        await db.SaveChangesAsync(cancellationToken);
        LogLoaded(toCreate.Count, file.Staff.Count - toCreate.Count, insertedCustomers, customerFile.Customers.Count - insertedCustomers);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sample identity: {Staff} staff inserted, {StaffSkipped} skipped; {Customers} customer users inserted, {CustomersSkipped} skipped")]
    private partial void LogLoaded(int staff, int staffSkipped, int customers, int customersSkipped);

    private sealed record IdentityFile(IReadOnlyList<StaffRow> Staff);

    private sealed record StaffRow(string Key, string Email, string FullName, IReadOnlyList<string> RoleCodes, bool AllLocations, IReadOnlyList<string> LocationCodes);

    private sealed record CustomersFile(IReadOnlyList<CustomerRow> Customers);

    private sealed record CustomerRow(string Key, string Phone);
}
