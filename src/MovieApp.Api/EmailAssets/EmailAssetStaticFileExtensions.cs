using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace MovieApp.Api.EmailAssets;

public static class EmailAssetStaticFileExtensions
{
    public static IApplicationBuilder UseEmailAssetStaticFiles(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return app;
        }

        var emailAssetsRoot = Path.Combine(webRoot, "email-assets");
        if (!Directory.Exists(emailAssetsRoot))
        {
            return app;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(emailAssetsRoot),
            RequestPath = "/email-assets",
            OnPrepareResponse = static context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            }
        });

        return app;
    }
}
