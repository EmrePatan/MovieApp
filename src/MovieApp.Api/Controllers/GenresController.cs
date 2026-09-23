using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Contracts.Genres;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/genres")]
public sealed class GenresController(IGenreService genreService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GenreResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GenreResponse>>> GetGenres(
        CancellationToken cancellationToken)
    {
        var genres = await genreService.GetAllAsync(Request.ResolveContentLocale(), cancellationToken);
        var response = genres
            .Select(genre => new GenreResponse(genre.Id, genre.Name))
            .ToList();

        return Ok(response);
    }
}
