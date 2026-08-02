namespace TDSBridge.Common.Middleware;

/// <summary>
/// Equivalent of ASP.NET Core's IMiddleware interface, for class-based middleware.
/// </summary>
public interface IMiddleware<TContext>
{
    Task InvokeAsync(TContext context, MiddlewareDelegate<TContext> next);
}