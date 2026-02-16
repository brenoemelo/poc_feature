namespace PoC.Shared.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; }

    public string? Cursor { get; init; }
    
    public int Count => Items.Count;

    public PagedResult(IEnumerable<T> items, string? cursor)
    {
        Items = items.ToList().AsReadOnly();
        Cursor = cursor;
    }
    
    // Additional constructor for deserialization if needed or direct list usage?
    // The JSON deserializer will set the Init property.
    // If deserializing from JSON, parameters are likely used if record/class has parameterized ctor.
    // But this logic `Items = items.ToList().AsReadOnly()` works.
    // However, if we change `Items` type to `IReadOnlyList<T>`, deserialization might fail if it can't instantiate it directly from array.
    // `List<T>` is safer for default deserialization.
    // But `IReadOnlyList<T>` is usually supported in STJ.
}
