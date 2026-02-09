namespace BlazorJSComponents;

/// <summary>
/// Allocates unique identifiers for embedding information in the DOM.
/// Uses a GUID for cross-scope uniqueness, then an incrementing integer per scope.
/// </summary>
internal sealed class UniqueIdAllocator
{
    private readonly string _scopeId = Guid.CreateVersion7().ToString("N");
    private int _next = 1;

    public string GetNextId()
        => $"{_scopeId}:{_next++}";
}
