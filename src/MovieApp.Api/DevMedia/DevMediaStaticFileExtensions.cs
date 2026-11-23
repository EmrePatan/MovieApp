using Microsoft.Extensions.FileProviders;
using MovieApp.Infrastructure.Storage;

namespace MovieApp.Api.DevMedia;

public static class DevMediaStaticFileExtensions
{
    /// <summary>
    /// Serves files written by <see cref="LocalDiskUserAvatarBlobStorage"/> at <c>/dev-media</c>.
    /// Development only; pair with <c>AvatarStorage:PublicBaseUrl = http://host:port/dev-media</c>.
    /// </summary>
    public static IApplicationBuilder UseDevMediaStaticFiles(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return app;
        }

        var root = LocalDiskUserAvatarBlobStorage.DefaultRootPath;
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = "/dev-media",
        });

        return app;
    }
}
