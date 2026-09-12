using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.RateLimiting;

internal static class ProductionRateLimitPolicyRegistration
{
    internal static void AddPolicies(RateLimiterOptions rateLimiterOptions, IConfiguration configuration)
    {
        var searchOptions = configuration
            .GetSection(SearchRateLimitOptions.SectionName)
            .Get<SearchRateLimitOptions>() ?? new SearchRateLimitOptions();

        var accountOptions = configuration
            .GetSection(AccountRateLimitOptions.SectionName)
            .Get<AccountRateLimitOptions>() ?? new AccountRateLimitOptions();

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.UnifiedSearch, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                SearchRateLimitPolicies.UnifiedSearch,
                searchOptions.UnifiedSearchPermitLimit,
                searchOptions.UnifiedSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.UnifiedSearch}"));

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.MovieSearch, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                SearchRateLimitPolicies.MovieSearch,
                searchOptions.MovieSearchPermitLimit,
                searchOptions.MovieSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.MovieSearch}"));

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.TvSearch, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                SearchRateLimitPolicies.TvSearch,
                searchOptions.TvSearchPermitLimit,
                searchOptions.TvSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.TvSearch}"));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.ChangePassword, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                AccountRateLimitPolicies.ChangePassword,
                accountOptions.PasswordChangePermitLimit,
                accountOptions.PasswordChangeWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.ChangeEmail, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                AccountRateLimitPolicies.ChangeEmail,
                accountOptions.EmailChangePermitLimit,
                accountOptions.EmailChangeWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.DeleteAccount, httpContext =>
            CreateDistributedPolicy(
                httpContext,
                AccountRateLimitPolicies.DeleteAccount,
                accountOptions.AccountDeletionPermitLimit,
                accountOptions.AccountDeletionWindowMinutes,
                AccountPartitionKeyFactory.Create));
    }

    private static RateLimitPartition<string> CreateDistributedPolicy(
        HttpContext httpContext,
        string policyName,
        int permitLimit,
        int windowMinutes,
        Func<HttpContext, string> partitionKeyFactory)
    {
        var store = httpContext.RequestServices.GetRequiredService<IRateLimitCounterStore>();
        var partitionKey = partitionKeyFactory(httpContext);

        return RateLimitPartition.Get(
            partitionKey,
            _ => new DistributedFixedWindowRateLimiter(
                store,
                $"{policyName}:{partitionKey}",
                permitLimit,
                TimeSpan.FromMinutes(Math.Max(1, windowMinutes))));
    }
}
