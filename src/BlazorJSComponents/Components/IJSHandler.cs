using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace BlazorJSComponents;

internal interface IJSHandler :
    IHandleAfterRender,
    IJSObjectReference
{
    void Render(RenderTreeBuilder builder);

    void SetArgs(object?[]? args);
}