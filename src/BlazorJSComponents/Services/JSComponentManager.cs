using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;

namespace BlazorJSComponents;

internal sealed class JSComponentManager
{
    private readonly ConcurrentDictionary<Type, string> _componentCollocatedJSPathCache = new();
    private readonly ConcurrentDictionary<Assembly, AssemblyCollocatedJSAttribute?> _assemblyAttrCache = new();

    public JsonSerializerOptions JsonSerializerOptions { get; }

    public JSComponentManager(IOptions<JSComponentOptions> options)
    {
        JsonSerializerOptions = options.Value.JsonSerializerOptions;

        if (MetadataUpdater.IsSupported)
        {
            BlazorJSComponentsMetadataUpdateHandler.TrackJSComponentManager(this);
        }
    }

    public string GetCollocatedComponentJSPath(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var type = component.GetType();
        return _componentCollocatedJSPathCache.GetOrAdd(type, ComputeCollocatedComponentJSPath);
    }

    private string ComputeCollocatedComponentJSPath(Type type)
    {
        var assembly = type.Assembly;

        var assemblyAttr = _assemblyAttrCache.GetOrAdd(
            assembly,
            static a => a.GetCustomAttribute<AssemblyCollocatedJSAttribute>())
            ?? throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' must be annotated with '{nameof(AssemblyCollocatedJSAttribute)}' " +
                $"to enable collocated JS discovery for component '{type.FullName}'.");

        var componentAttr = type.GetCustomAttribute<DiscoverCollocatedJSAttribute>()
            ?? throw new InvalidOperationException(
                $"Component '{type.FullName}' must be annotated with '{nameof(DiscoverCollocatedJSAttribute)}' " +
                $"to infer its collocated JS file path.");

        var razorFilePath = componentAttr.RazorFilePath
            ?? throw new InvalidOperationException(
                $"'{nameof(DiscoverCollocatedJSAttribute)}' on '{type.FullName}' did not provide a valid razor file path.");

        var prefix = assemblyAttr.CallerFileNamePathPrefix;

        if (!razorFilePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Razor file path '{razorFilePath}' does not start with expected prefix '{prefix}'.");
        }

        var relativePath = razorFilePath[prefix.Length..];

        return $"./{assemblyAttr.StaticWebAssetBasePath}{relativePath}.js";
    }

    public void ClearCache(Type[]? updatedTypes)
    {
        if (updatedTypes is null)
        {
            _componentCollocatedJSPathCache.Clear();
            _assemblyAttrCache.Clear();
            return;
        }

        foreach (var type in updatedTypes)
        {
            _componentCollocatedJSPathCache.TryRemove(type, out _);
        }
    }
}
