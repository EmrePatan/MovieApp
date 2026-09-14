using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.PushNotifications;

namespace MovieApp.Infrastructure.PushNotifications;

public static class ExpoPushClientServiceCollectionExtensions
{
    public static IServiceCollection AddExpoPushClient(this IServiceCollection services)
    {
        services.AddHttpClient<IExpoPushClient, ExpoPushClient>(client =>
        {
            client.BaseAddress = new Uri("https://exp.host/--/api/v2/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        return services;
    }
}
