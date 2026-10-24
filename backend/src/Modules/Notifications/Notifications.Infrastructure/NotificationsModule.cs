using FreshOrbit.Customers.Public.Events;
using FreshOrbit.Payments.Public.Events;
using FreshOrbit.Ordering.Public.Events;
using FreshOrbit.Notifications.Application.Handlers;
using FluentValidation;
using FreshOrbit.Identity.Public;
using FreshOrbit.Identity.Public.Events;
using FreshOrbit.Notifications.Application.Inbox;
using FreshOrbit.Notifications.Application.Queueing;
using FreshOrbit.Notifications.Application.Sending;
using FreshOrbit.Notifications.Application.Shared;
using FreshOrbit.Notifications.Application.Templates;
using FreshOrbit.Notifications.Domain.Shared;
using FreshOrbit.Notifications.Infrastructure.Endpoints;
using FreshOrbit.Notifications.Infrastructure.Persistence;
using FreshOrbit.Notifications.Infrastructure.Persistence.Repositories;
using FreshOrbit.Notifications.Infrastructure.Seeding;
using FreshOrbit.Notifications.Infrastructure.Senders;
using FreshOrbit.Notifications.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.EntityFramework.Seeding;
using FreshOrbit.SharedKernel.Jobs;
using FreshOrbit.SharedKernel.Web.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FreshOrbit.Notifications.Infrastructure;

/// <summary>Composition root of the Notifications module.</summary>
public sealed class NotificationsModule : IModule
{
    /// <summary>
    /// The sample-data-only public demo (<c>docs/runbooks/render-demo.md</c>, FreshOrbit.Hosting <c>DemoSetup</c>): like
    /// Development it uses the development SMS sender and maps <c>/dev/sms</c>; the hosts refuse it on any database
    /// the demo did not create.
    /// </summary>
    internal const string DemoEnvironmentName = "Demo";

    public string Name => "Notifications";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleModel<NotificationsModuleModel>();

        var options = configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>() ?? new NotificationsOptions();
        services.AddSingleton(options);
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);

        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        services.AddScoped<NotificationComposer>();
        services.AddScoped<INotificationsApi>(provider => provider.GetRequiredService<NotificationComposer>());
        services.AddScoped<IOtpSender, OtpSender>();
        services.AddScoped<MessageDispatcher>();
        services.AddScoped<InboxService>();
        services.AddScoped<TemplateAdminService>();
        services.AddScoped<CustomerNotifier>();
        services.AddEventHandler<OrderPlaced, NotifyOrderPlaced>();
        services.AddEventHandler<OrderConfirmed, NotifyOrderConfirmed>();
        services.AddEventHandler<OrderStatusChanged, NotifyOrderStatusChanged>();
        services.AddEventHandler<OrderPacked, NotifyOrderPacked>();
        services.AddEventHandler<OrderDispatched, NotifyOrderDispatched>();
        services.AddEventHandler<OrderDelivered, NotifyOrderDelivered>();
        services.AddEventHandler<OrderCancelled, NotifyOrderCancelled>();
        services.AddEventHandler<OrderPaymentFailed, NotifyOrderPaymentFailed>();
        services.AddEventHandler<SubstitutionProposed, NotifySubstitutionProposed>();
        services.AddEventHandler<ComplaintRaised, NotifyComplaintRaised>();
        services.AddEventHandler<ComplaintDecided, NotifyComplaintDecided>();
        services.AddEventHandler<PaymentCaptured, NotifyPaymentCaptured>();
        services.AddEventHandler<PaymentFailed, NotifyPaymentFailed>();
        services.AddEventHandler<RefundApproved, NotifyRefundApproved>();
        services.AddEventHandler<RefundSucceeded, NotifyRefundSucceeded>();
        services.AddEventHandler<CustomerDeleted, DropDeviceTokensOnCustomerDeleted>();
        services.AddEventHandler<StockAlertTriggered, NotifyStockAlertTriggered>();
        services.AddEventHandler<StaffAccountLocked, NotifyStaffAccountLocked>();
        services.AddScoped<IValidator<InboxRequest>, InboxRequestValidator>();
        services.AddScoped<IValidator<RegisterDeviceTokenRequest>, RegisterDeviceTokenRequestValidator>();
        services.AddScoped<IValidator<ReplacePreferencesRequest>, ReplacePreferencesRequestValidator>();

        // Adapters (NOT-R11): TryAdd so a host or test can register a real provider first.
        // SMS: Notifications:Sms:Provider; the development sender is refused outside Development/Testing (SEC-1).
        services.AddFreshOrbitSms();
        services.TryAddSingleton<IPushSender, LoggingPushSender>();
        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]))
        {
            services.TryAddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        }

        services.AddScoped<SendNotificationsJob>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRecurringJobRegistration, NotificationsJobs>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IReferenceDataLoader, NotificationsReferenceDataLoader>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        NotificationsEndpoints.Map(endpoints);
        var environment = endpoints.ServiceProvider.GetService<IHostEnvironment>();
        if (environment is not null && (environment.IsDevelopment() || environment.IsEnvironment("Testing") || environment.IsEnvironment(DemoEnvironmentName)))
        {
            NotificationsEndpoints.MapDevelopment(endpoints);
        }
    }
}
