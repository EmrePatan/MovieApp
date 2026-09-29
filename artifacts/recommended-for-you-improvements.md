# Recommended For You — what changed

Home “Senin İçin Önerilen” / Recommended For You now unlocks only from positive taste, scores movies and TV together, and keeps a hard diversity window after Hot This Week is removed.

No live user history was used. Behavior is covered by unit tests and PostgreSQL integration tests.

## Unlock

Recommended For You stays hidden until the user has at least `MinimumPersonalizationInteractions` (default 3) **positive** taste signals.

Positive: favorite, rating at or above `MildRatingMinScore` (default 6), watchlist, watched, TV follow.

A rating below that threshold does not count toward the unlock. It pushes matching genres and keywords down by `LowRatingSignalWeight` (default -0.6). If the same title is both a favorite and rated below the mild threshold, the low rating wins for taste polarity and the title stays out of the candidate pool.

Search history still does not unlock the rail. Cold start on `/api/home/personalized` still omits the section.

## Candidates

`type=all` splits the candidate budget in half between movies and TV. `CandidateMinorityTypeMinimum` (default 100) is a floor and is clamped so it never exceeds half of `MaximumCandidates`. Unused movie slots go to TV; if TV cannot fill its half, the rest is filled with more movies.

`type=movie` and `type=tv` use the full budget for that type. Home passes the requested type into retrieval and scoring instead of filtering a mixed top 10.

When two or more genres have positive taste, each keeps at least `CandidateMinPerGenre` (default 8) vote-leaders before the global vote-count fill (up to 8 genres). Titles under `CandidateMinVoteCount` (default 20) are excluded. Zero disables that floor.

## Diversity (franchise and genre stacking)

Caps are hard. Items that miss a cap are dropped. Score order is kept among items that pass, except a light primary-genre interleave: the same first genre id is not placed back-to-back when a later cap-passing title has a different primary genre. Nothing outside the scored pool is shuffled in.

| Knob | Default | Effect on a rail of 10 |
|---|---|---|
| `DiversityMaxPerCollection` | 1 | At most one title per TMDB collection (no 2–3 Avengers from the same collection). |
| `DiversityMaxPerGenre` | 3 | At most 3 titles sharing one of the first two genre ids. |
| `DiversityMaxPerFranchiseFamily` | 2 | At most 2 titles that share a configured franchise keyword. |

There is no production-company table. Franchise family uses exact catalog keyword names in `DiversityFranchiseKeywordNames` (case-insensitive, only when that keyword is synced): marvel cinematic universe, dc extended universe, star wars, james bond. Unmatched names do nothing. Collection cap covers direct sequels; the keyword cap covers cross-collection families such as the MCU.

Home asks for the full personalized scored pool (`MaximumCandidates`, at least the hero window of `sectionSize + HeroSectionSize + HomeRecommendationSurplus`). It removes Hot This Week ids first, applies the caps to that pool, lightly interleaves primary genres, then takes `sectionSize`. If the caps leave fewer than `sectionSize` titles, later items in the same scored pool that still pass the caps fill the rail. Global popular, trending, cold-start, and unscored catalog titles are not used as padding. Overlap with the hero leaves the rail when anything else scored high enough remains. `/api/recommendations` and `/api/recommendations/home` still apply the same caps before pagination, without the home interleave.

A library that is almost entirely one genre, collection, or franchise can return a shorter rail. That is the cap working.

## Other

- `PersonalizedPersonWeight` is unused. Movie hydration does not load cast, and behavior similarity passes empty person lists. Wiring it would only affect TV.
- `Reason` is still not on `HomeItem` (mobile is unchanged). `/api/recommendations` reasons now use the strongest personal signal (genre, keyword, or behavior), not the first watched title in list order.
- Personalized recommendation cache generation is `v6`. Home response cache is `v8`. The home recommendation key includes content type, result limit, and whether the payload is already diversified (`div`) or a raw surplus for hero removal (`raw`).

## Tests that lock the audit failures

- TV titles remain when at least 500 matching movies exist.
- Ratings below 6 alone do not unlock the home rail.
- Collection cap is 1 inside the top 10.
- One genre cannot fill the rail past the genre cap.
- `type=tv` fills the section from TV candidates.
- Hero dedup drops an overlapping id when surplus items exist.
