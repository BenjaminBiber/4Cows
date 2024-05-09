using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

public interface IViewportService
{
    Task<ViewportSize> GetViewportSize();
}

public class ViewportService : IViewportService
{
    private readonly IJSRuntime _jsRuntime;

    public ViewportService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<ViewportSize> GetViewportSize()
    {
        var windowSize = await _jsRuntime.InvokeAsync<ViewportSize>("getViewportSize");
        return windowSize;
    }
}

public class ViewportSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}
