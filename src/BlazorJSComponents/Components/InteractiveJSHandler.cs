using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;

namespace BlazorJSComponents;

internal sealed class InteractiveJSHandler(
    string src,
    string? key,
    IJSRuntime jsRuntime) : IJSHandler
{
    private object?[]? _args;

    private TaskCompletionSource<object?>? _initTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int? _instanceId;
    private Task? _onAfterRenderTask;

    public async ValueTask DisposeAsync()
    {
        if (_instanceId is not int id)
            return;

        try
        {
            await WaitForPendingRenderAsync();
            await jsRuntime.InvokeVoidAsync(
                "__blazorScript.disposeJSComponent",
                id);
        }
        catch (JSDisconnectedException)
        {
            // Ignore
        }
    }

    public ValueTask<TValue> InvokeAsync<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.PublicProperties)]
    TValue>(string identifier, object?[]? args)
        => InvokeCoreAsync<TValue>(identifier, default, args);

    public ValueTask<TValue> InvokeAsync<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields |
            DynamicallyAccessedMemberTypes.PublicProperties)]
    TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeCoreAsync<TValue>(identifier, cancellationToken, args);

    public async Task OnAfterRenderAsync()
    {
        _onAfterRenderTask = OnAfterRenderCoreAsync();
        try
        {
            await _onAfterRenderTask;
        }
        finally
        {
            _onAfterRenderTask = null;
        }
    }

    public void Render(RenderTreeBuilder builder)
    {
        // No DOM output
    }

    public void SetArgs(object?[]? args) => _args = args;

    private async ValueTask<TValue> InvokeCoreAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
    {
        await WaitForPendingRenderAsync();

        if (_instanceId is not int id)
        {
            throw new InvalidOperationException(
                $"There is no JS component associated with the '{nameof(JS)}' instance");
        }

        return await jsRuntime.InvokeAsync<TValue>(
            "__blazorScript.invokeJSComponentMethod",
            cancellationToken,
            id,
            identifier,
            args);
    }

    private async Task OnAfterRenderCoreAsync()
    {
        if (_initTcs is not null)
        {
            _instanceId = await jsRuntime.InvokeAsync<int>(
                "__blazorScript.getOrCreateJSComponent",
                0,
                src,
                key);

            _initTcs.TrySetResult(null);
            _initTcs = null;
        }

        if (_instanceId is int id)
        {
            await jsRuntime.InvokeVoidAsync(
                "__blazorScript.setJSComponentParameters",
                id,
                _args);
        }
    }

    private async Task WaitForPendingRenderAsync()
    {
        if (_initTcs is { } init)
            await init.Task;

        if (_onAfterRenderTask is { } renderTask)
            await renderTask;
    }
}