namespace TDSBridge.Common.Middleware;

/// <summary>
/// Optional: implement on your context type so class-based middleware
/// resolved via UseMiddleware&lt;T&gt; gets scoped DI (e.g. a scoped DbContext
/// per message) rather than the root provider.
/// </summary>
public interface IHasServiceProvider
{
    IServiceProvider Services { get; }
}
