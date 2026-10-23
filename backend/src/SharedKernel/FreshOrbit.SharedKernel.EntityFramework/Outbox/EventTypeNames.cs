using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>
/// Event type names stored in <c>platform.outbox.event_type</c>: <c>Full.Type.Name, AssemblyName</c> without
/// version, culture or public key, so a redeploy with a new assembly version still resolves stored events.
/// </summary>
public static class EventTypeNames
{
    public static string Format(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return $"{eventType.FullName}, {eventType.Assembly.GetName().Name}";
    }

    public static Type? Resolve(string eventType)
    {
        var type = Type.GetType(eventType, throwOnError: false);
        return type is not null && typeof(DomainEvent).IsAssignableFrom(type) && !type.IsAbstract ? type : null;
    }
}
