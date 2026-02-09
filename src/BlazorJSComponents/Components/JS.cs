using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace BlazorJSComponents;

/// <summary>
/// Represents some JavaScript to dynamically import.
/// This may or may not refer to a JavaScript component.
/// </summary>
public sealed class JS :
    IComponent,
    IHandleAfterRender,
    IJSObjectReference
{
    private IJSHandler? _jsHandler;
    private RenderHandle _renderHandle;

    [Parameter] public object?[]? Args { get; set; }
    [Parameter] public IComponent? For { get; set; }
    [Parameter] public string? Key { get; set; }
    [Parameter] public string? Src { get; set; }
    [Inject] private JSComponentManager JSComponentManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private UniqueIdAllocator UniqueIdAllocator { get; set; } = default!;

    void IComponent.Attach(RenderHandle renderHandle)
        => _renderHandle = renderHandle;

    ValueTask IAsyncDisposable.DisposeAsync()
        => _jsHandler?.DisposeAsync() ?? ValueTask.CompletedTask;

    public ValueTask<TValue> InvokeAsync<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.PublicProperties)]
    TValue>(string identifier, object?[]? args)
        => HandlerOrThrow().InvokeAsync<TValue>(identifier, args);

    public ValueTask<TValue> InvokeAsync<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.PublicProperties)]
    TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => HandlerOrThrow().InvokeAsync<TValue>(identifier, cancellationToken, args);

    Task IHandleAfterRender.OnAfterRenderAsync()
        => HandlerOrThrow().OnAfterRenderAsync();

    Task IComponent.SetParametersAsync(ParameterView parameters)
    {
        var (oldSrc, oldFor, oldKey) = (Src, For, Key);
        (Src, For, Key, Args) = ExtractParameters(in parameters);

        if (_jsHandler is null)
        {
            var src = (Src, For) switch
            {
                (not null, null) => Src,
                (null, not null) => JSComponentManager.GetCollocatedComponentJSPath(For),
                (null, null) => throw MustSpecifyEitherSrcOrFor(),
                _ => throw CannotSpecifyBothSrcAndFor(),
            };

            _jsHandler = _renderHandle.RendererInfo.IsInteractive
                ? new InteractiveJSHandler(src!, Key, JSRuntime)
                : new StaticJSHandler(
                    src!,
                    Key,
                    mayBecomeInteractive: _renderHandle.RenderMode is not null,
                    UniqueIdAllocator,
                    JSComponentManager.JsonSerializerOptions);
        }
        else
        {
            ThrowIfChanged(oldSrc, Src);
            ThrowIfChanged(oldFor, For);
            ThrowIfChanged(oldKey, Key);
        }

        _jsHandler.SetArgs(Args);
        _renderHandle.Render(_jsHandler.Render);
        return Task.CompletedTask;
    }

    private static InvalidOperationException CannotSpecifyBothSrcAndFor()
        => new($"Must specify one of '{nameof(Src)}' or '{nameof(For)}', but not both.");

    private static (string? Src, IComponent? For, string? Key, object?[]? Args)
        ExtractParameters(in ParameterView parameters)
    {
        string? src = null;
        IComponent? @for = null;
        string? key = null;
        object?[]? args = null;

        foreach (var p in parameters)
        {
            switch (p.Name)
            {
                case nameof(Src): src = (string?)p.Value; break;
                case nameof(For): @for = (IComponent?)p.Value; break;
                case nameof(Key): key = (string?)p.Value; break;
                case nameof(Args): args = (object?[]?)p.Value; break;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected {nameof(JS)} parameter '{p.Name}'.");
            }
        }

        return (src, @for, key, args);
    }

    private static InvalidOperationException MustSpecifyEitherSrcOrFor()
        => new($"Must specify either '{nameof(Src)}' or '{nameof(For)}'.");

    private static void ThrowIfChanged(
        object? oldValue,
        object? currentValue,
        [CallerArgumentExpression(nameof(currentValue))] string? paramName = null)
    {
        if (!Equals(oldValue, currentValue))
        {
            throw new InvalidOperationException(
                $"Cannot dynamically change the value of the '{paramName}' parameter.");
        }
    }

    private IJSHandler HandlerOrThrow()
        => _jsHandler ?? throw new InvalidOperationException(
            $"This operation is not permitted until parameters are set on the {nameof(JS)} instance.");
}