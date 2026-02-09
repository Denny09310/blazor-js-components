using System.Text.Json.Serialization;

namespace BlazorJSComponents;

/// <summary>
/// Represents a scope for element references to be passed to JavaScript components.
/// </summary>
[JsonConverter(typeof(JSElementReferenceScopeJsonConverter))]
public sealed class JSElementReferenceScope
{
    internal readonly string Id;

    /// <summary>
    /// Creates a new <see cref="JSElementReferenceScope"/>.
    /// </summary>
    public static JSElementReferenceScope Create()
        => new(Guid.CreateVersion7().ToString("N"));

    internal JSElementReferenceScope(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Given a JS property name, returns a unique identifier for the specified element.
    /// </summary>
    public string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return $"{Id}-{name}";
        }
    }
}
