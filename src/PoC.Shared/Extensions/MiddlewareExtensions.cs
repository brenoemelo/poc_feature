using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace PoC.Shared.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseDoubleSlashFix(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value?.Contains("//") == true)
            {
                var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("PoC.Shared.Middleware.DoubleSlashFix");
                var originalPath = context.Request.Path.Value;
                context.Request.Path = originalPath.Replace("//", "/");
                logger?.LogDebug("Fixed double slash in path. Original: {OriginalPath}, New: {NewPath}", originalPath, context.Request.Path);
            }
            await next(context);
        });
    }
}
