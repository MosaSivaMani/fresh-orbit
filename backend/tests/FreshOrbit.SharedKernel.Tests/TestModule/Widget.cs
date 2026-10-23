using FreshOrbit.SharedKernel.Domain;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Events;

namespace FreshOrbit.SharedKernel.Tests.TestModule;

public enum WidgetStatus
{
    Draft,
    InReview,
}

/// <summary>Test aggregate exercising row version, audit columns, enum codes and domain events.</summary>
public sealed class Widget : AggregateRoot
{
    private Widget()
    {
        Name = string.Empty;
    }

    private Widget(string name)
    {
        Name = name;
        Status = WidgetStatus.Draft;
    }

    public string Name { get; private set; }

    public WidgetStatus Status { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static Widget Create(string name)
    {
        var widget = new Widget(name);
        widget.Raise(new WidgetCreated(widget.Id, name));
        return widget;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("WIDGET_NAME_REQUIRED", "Name is required.", "name");
        }

        Name = name;
        Raise(new WidgetRenamed(Id, name));
    }

    public void SubmitForReview(DateTimeOffset at)
    {
        Status = WidgetStatus.InReview;
        ReviewedAt = at;
    }

    public void Explode() => Raise(new WidgetExploded(Id));
}

public sealed record WidgetCreated(Guid WidgetId, string Name) : DomainEvent;

public sealed record WidgetRenamed(Guid WidgetId, string Name) : DomainEvent;

public sealed record WidgetExploded(Guid WidgetId) : DomainEvent;

/// <summary>Written by event handlers so tests can see a handler's changes were saved.</summary>
public sealed class WidgetLog : Entity
{
    public WidgetLog(Guid widgetId, string entry)
    {
        WidgetId = widgetId;
        Entry = entry;
    }

    public Guid WidgetId { get; private set; }

    public string Entry { get; private set; }
}
