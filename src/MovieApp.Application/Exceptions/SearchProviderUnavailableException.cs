namespace MovieApp.Application.Exceptions;

public sealed class SearchProviderUnavailableException()
    : Exception("Search provider is temporarily unavailable.");
