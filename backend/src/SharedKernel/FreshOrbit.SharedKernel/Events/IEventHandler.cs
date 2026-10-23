namespace FreshOrbit.SharedKernel.Events;

/// <summary>
/// Handles a domain event delivered from the outbox. Runs in its own DI scope; the dispatcher saves the
/// handler's tracked changes together with its inbox row, so a handler must not call SaveChanges itself.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain event handler contract name agreed for all modules.")]
public interface IEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
