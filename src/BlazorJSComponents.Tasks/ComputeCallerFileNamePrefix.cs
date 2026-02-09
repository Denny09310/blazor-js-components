using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace BlazorJSComponents.Tasks;

public sealed class ComputeCallerFileNamePrefix : Task
{
    [Required]
    public ITaskItem[] SourceRoot { get; set; } = default!;

    [Required]
    public string ProjectDir { get; set; } = default!;

    [Output]
    public string? CallerFileNamePrefix { get; set; }

    public override bool Execute()
    {
        if (SourceRoot.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No SourceRoot items provided.");
        }

        if (string.IsNullOrWhiteSpace(ProjectDir))
        {
            Log.LogError("ProjectDir was not provided or is empty.");
            return false;
        }

        Log.LogMessage(MessageImportance.Low, "Creating path map:");

        var builder = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>(SourceRoot.Length);

        foreach (var item in SourceRoot)
        {
            var root = item.ItemSpec;
            var mappedPath = item.GetMetadata("MappedPath");

            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(mappedPath))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "  Skipping source root '{0}' with mapped path '{1}' because either value was empty.",
                    root,
                    mappedPath);

                continue;
            }

            Log.LogMessage(
                MessageImportance.Low,
                "  Adding source root '{0}' with mapped path '{1}'.",
                root,
                mappedPath);

            builder.Add(new(root, mappedPath));
        }

        var pathMap = builder.MoveToImmutable();

        try
        {
            var resolver = new SourceFileResolver(
                searchPaths: [],
                baseDirectory: null,
                pathMap: pathMap);

            CallerFileNamePrefix = resolver.NormalizePath(ProjectDir, baseFilePath: null);

            Log.LogMessage(
                MessageImportance.Low,
                "Computed CallerFileNamePrefix: '{0}'",
                CallerFileNamePrefix);
        }
        catch (System.Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: false);
            return false;
        }

        return !Log.HasLoggedErrors;
    }
}
