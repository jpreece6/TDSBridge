using Microsoft.Extensions.DependencyInjection;

namespace TDSBridge.Common.Middleware;

public delegate Task MiddlewareDelegate<TContext>(TContext context);

/// <summary>
/// Equivalent of IApplicationBuilder. Collects middleware registrations
/// and composes them into a single delegate.
/// </summary>
public class MiddlewarePipelineBuilder<TContext>
{

    private readonly List<Func<MiddlewareDelegate<TContext>, MiddlewareDelegate<TContext>>> _components = new();
 
    /// <summary>
    /// Register an inline middleware, same shape as app.Use(next => async ctx => {...}).
    /// </summary>
    public MiddlewarePipelineBuilder<TContext> Use(
        Func<MiddlewareDelegate<TContext>, MiddlewareDelegate<TContext>> middleware)
    {
        _components.Add(middleware);
        return this;
    }

    /// <summary>
    /// Register a class-based middleware resolved via DI (like app.UseMiddleware&lt;T&gt;()).
    /// Supports constructor injection; a new instance is created per pipeline build,
    /// but you can also resolve it per-request/per-item by capturing IServiceProvider.
    /// </summary>
    public MiddlewarePipelineBuilder<TContext> UseMiddleware<TMiddleware>(IServiceProvider rootProvider)
        where TMiddleware : IMiddleware<TContext>
    {
        return Use(next => async context =>
        {

            // Resolve from the current scope if the context carries one, else fall back.
            var provider = (context as IHasServiceProvider)?.Services ?? rootProvider;
            var middleware = ActivatorUtilities.CreateInstance<TMiddleware>(provider);
            await middleware.InvokeAsync(context, next);
        });
    }

    /// <summary>
    /// Terminal delegate used if nothing else short-circuits the pipeline.
    /// Equivalent of the 404 fallback at the end of an ASP.NET Core pipeline.
    /// </summary>
    public MiddlewarePipelineBuilder<TContext> Run(Func<TContext, Task> terminal)
    {
        return Use(_ => terminal.Invoke);
    }
 
    /// <summary>
    /// Composes all registered middleware into a single MiddlewareDelegate,
    /// wiring each one's "next" to the next registered component, wrapped in reverse.
    /// </summary>
    public MiddlewareDelegate<TContext> Build()
    {
        MiddlewareDelegate<TContext> pipeline = _ => Task.CompletedTask;
 
        for (int i = _components.Count - 1; i >= 0; i--)
        {
            pipeline = _components[i](pipeline);
        }
 
        return pipeline;
    }
}