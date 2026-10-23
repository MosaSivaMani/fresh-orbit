namespace FreshOrbit.SharedKernel.EntityFramework.Platform;

/// <summary>A row inserted by a sample data loader (<c>schema.table</c> + <c>id</c>), so it can be purged.</summary>
public sealed class SampleRecord
{
    public SampleRecord(string tableName, Guid recordId)
    {
        TableName = tableName;
        RecordId = recordId;
    }

    public string TableName { get; private set; }

    public Guid RecordId { get; private set; }
}
