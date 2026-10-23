using FreshOrbit.SharedKernel.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.EntityFramework.Outbox;

/// <summary>Resolves and calls <see cref="IEventHandler{TEvent}"/> implementations for a runtime event type.</summary>
internal abstract class EventHandlerInvoker
{
    public static EventHandlerInvoker For(Type eventType)
        => (EventHandlerInvoker)Activator.CreateInstance(typeof(Typed<>).MakeGenericType(eventType))!;

    public abstract IReadOnlyList<object> ResolveHandlers(IServiceProvider services);

    public abstract Task InvokeAsync(object handler, DomainEvent domainEvent, CancellationToken cancellationToken);

    private sealed class Typed<TEvent> : EventHandlerInvoker
        where TEvent : DomainEvent
    {
        public override IReadOnlyList<object> ResolveHandlers(IServiceProvider services)
            => [.. services.GetServices<IEventHandler<TEvent>>().Cast<object>()];

        public override Task InvokeAsync(object handler, DomainEvent domainEvent, CancellationToken cancellationToken)
            => ((IEventHandler<TEvent>)handler).HandleAsync((TEvent)domainEvent, cancellationToken);
    }
}
