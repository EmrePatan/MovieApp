using System.Net;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbTvShowDataProvider(
    TmdbApiClient apiClient,
    IOptions<AdvancedDiscoverOptions>? advancedDiscoverOptions = null,
    IOptions<NewReleasesOptions>? newReleasesOptions = null) : ITvShowDataProvider
{
    public async Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetCanonicalAsync<TmdbTvSearchResponseJson>(
            $"search/tv?query={encodedQuery}&include_adult=false&page={page}",
            cancellationToken);

        if (response is null)
        {
            return new TvShowProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbTvShowMapper.ToSummary)
            .ToList();

        return new TvShowProviderSearchResult(
            results,
            response.Page == 0 ? page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(
            criteria,
            NewReleasesDiscoverConstraints.ForTvShows(newReleasesOptions));
        var response = await apiClient.GetCanonicalAsync<TmdbTvSearchResponseJson>(
            $"discover/tv?{query}",
            cancellationToken);

        if (response is null)
        {
            return new TvShowProviderSearchResult(
                [],
                criteria.Page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbTvShowMapper.ToSummary)
            .ToList();

        return new TvShowProviderSearchResult(
            results,
            response.Page == 0 ? criteria.Page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildTvQuery(
            criteria,
            TmdbAdvancedDiscoverVoteFloor.Resolve(advancedDiscoverOptions));
        var response = await apiClient.GetCanonicalAsync<TmdbTvSearchResponseJson>(
            $"discover/tv?{query}",
            cancellationToken);

        if (response is null)
        {
            return new TvShowProviderSearchResult(
                [],
                criteria.Page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbTvShowMapper.ToSummary)
            .ToList();

        return new TvShowProviderSearchResult(
            results,
            response.Page == 0 ? criteria.Page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<TvShowProviderDetails?> GetTvShowAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default)
    {
        if (!TmdbExternalIdFormatter.TryParseExternalId(externalId, out var tmdbId))
        {
            return null;
        }

        try
        {
            var response = await apiClient.GetCanonicalAsync<TmdbTvDetailsResponseJson>(
                BuildTvShowDetailsPath(tmdbId, includeKeywords),
                cancellationToken);

            return response is null ? null : TmdbTvShowMapper.ToDetails(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string BuildTvShowDetailsPath(int tmdbId, bool includeKeywords)
    {
        var appendParts = new List<string> { "external_ids", "alternative_titles", "translations", "images" };
        if (includeKeywords)
        {
            appendParts.Add("keywords");
        }

        var path = $"tv/{tmdbId}?append_to_response={string.Join(',', appendParts)}";
        return TmdbRequestPath.WithIncludeImageLanguage(
            path,
            SupportedArtworkLanguageKeys.BuildTmdbIncludeImageLanguageParameter());
    }

    public async Task<SeasonProviderDetails?> GetSeasonAsync(
        string externalTvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (!TmdbExternalIdFormatter.TryParseExternalId(externalTvShowId, out var tmdbId))
        {
            return null;
        }

        try
        {
            var response = await apiClient.GetCanonicalAsync<TmdbTvSeasonDetailsResponseJson>(
                $"tv/{tmdbId}/season/{seasonNumber}",
                cancellationToken);

            return response is null
                ? null
                : TmdbTvShowMapper.ToSeasonDetails(externalTvShowId, response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<EpisodeProviderDetails?> GetEpisodeAsync(
        string externalTvShowId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        if (!TmdbExternalIdFormatter.TryParseExternalId(externalTvShowId, out var tmdbId))
        {
            return null;
        }

        try
        {
            var response = await apiClient.GetCanonicalAsync<TmdbTvEpisodeJson>(
                $"tv/{tmdbId}/season/{seasonNumber}/episode/{episodeNumber}?append_to_response=external_ids",
                cancellationToken);

            return response is null
                ? null
                : TmdbTvShowMapper.ToEpisodeDetails(externalTvShowId, seasonNumber, response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
