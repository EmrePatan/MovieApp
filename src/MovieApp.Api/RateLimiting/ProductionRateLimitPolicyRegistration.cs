using Microsoft.AspNetCore.RateLimiting;
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

        var tvShowFollowOptions = configuration
            .GetSection(TvShowFollowRateLimitOptions.SectionName)
            .Get<TvShowFollowRateLimitOptions>() ?? new TvShowFollowRateLimitOptions();

        var reviewTranslationOptions = configuration
            .GetSection(ReviewTranslationRateLimitOptions.SectionName)
            .Get<ReviewTranslationRateLimitOptions>() ?? new ReviewTranslationRateLimitOptions();

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.UnifiedSearch, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                SearchRateLimitPolicies.UnifiedSearch,
                searchOptions.UnifiedSearchPermitLimit,
                searchOptions.UnifiedSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.UnifiedSearch}"));

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.MovieSearch, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                SearchRateLimitPolicies.MovieSearch,
                searchOptions.MovieSearchPermitLimit,
                searchOptions.MovieSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.MovieSearch}"));

        rateLimiterOptions.AddPolicy(SearchRateLimitPolicies.TvSearch, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                SearchRateLimitPolicies.TvSearch,
                searchOptions.TvSearchPermitLimit,
                searchOptions.TvSearchWindowMinutes,
                context => $"{ClientIpResolver.GetClientIpAddress(context)}:{SearchRateLimitPolicies.TvSearch}"));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.ChangePassword, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                AccountRateLimitPolicies.ChangePassword,
                accountOptions.PasswordChangePermitLimit,
                accountOptions.PasswordChangeWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.ChangeEmail, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                AccountRateLimitPolicies.ChangeEmail,
                accountOptions.EmailChangePermitLimit,
                accountOptions.EmailChangeWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(AccountRateLimitPolicies.DeleteAccount, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                AccountRateLimitPolicies.DeleteAccount,
                accountOptions.AccountDeletionPermitLimit,
                accountOptions.AccountDeletionWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(TvShowFollowRateLimitPolicies.Mutation, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                TvShowFollowRateLimitPolicies.Mutation,
                tvShowFollowOptions.MutationPermitLimit,
                tvShowFollowOptions.MutationWindowMinutes,
                AccountPartitionKeyFactory.Create));

        rateLimiterOptions.AddPolicy(ReviewTranslationRateLimitPolicies.Translation, httpContext =>
            DistributedRateLimitPolicyFactory.CreatePolicy(
                httpContext,
                ReviewTranslationRateLimitPolicies.Translation,
                reviewTranslationOptions.PermitLimit,
                reviewTranslationOptions.WindowMinutes,
                AccountPartitionKeyFactory.Create));
    }
}
