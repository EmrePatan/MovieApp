#!/usr/bin/env python3
"""Offline, read-only recommendation retrieval diagnostic.

Input: a JSON export with *anonymized* profiles and catalog candidates.
No database connection, no writes, no production calls. This measures retrieval
loss and keyword coverage, NOT the exact C# ranking engine or real NDCG.

Example:
  python3 tools/recommendation_retrieval_audit.py --self-test
  python3 tools/recommendation_retrieval_audit.py --input /path/to/staging-export.json

Schema:
{
  "profiles": [{"id":"anonymous-1","genre_ids":["drama"],
                "keyword_weights":{"time-loop":1.0,"mystery":0.8},
                "excluded_ids":["movie-1"]}],
  "candidates": [{"id":"movie-2","type":"movie","genre_ids":["sci-fi"],
                  "keyword_ids":["time-loop"],"vote_count":200,
                  "vote_average":7.9,"eligible":true}]
}
`eligible` must be computed by the exporter according to the current
release-date, min-votes and content eligibility rules; never infer eligibility.
"""
import argparse
import json
import math
from pathlib import Path


def keyword_score(candidate, weights):
    ids = set(candidate.get("keyword_ids", []))
    if not ids:
        return 0.0
    overlap = sum(float(weights.get(k, 0)) for k in ids)
    return max(-1.0, min(1.0, overlap / math.sqrt(len(ids))))


def ordered(candidates):
    return sorted(candidates, key=lambda c: (
        -int(c["vote_count"]), -float(c["vote_average"]), str(c["id"])))


def audit_profile(profile, candidates, budget=500, top_k=10):
    genres = set(profile.get("genre_ids", []))
    weights = profile.get("keyword_weights", {})
    excluded = set(profile.get("excluded_ids", []))
    eligible = [c for c in candidates if c.get("eligible") is True
                and c["id"] not in excluded]
    # Mirrors the baseline's *hard genre predicate and popularity-first
    # truncation* only. Not an exact simulation of its stratified selection.
    genre_pool = [c for c in eligible
                  if not genres or genres.intersection(c.get("genre_ids", []))]
    baseline_pool = ordered(genre_pool)[:budget]
    baseline_ids = {c["id"] for c in baseline_pool}
    # Cross-genre retrieval opportunity: keyword matches absent from baseline.
    cross = [c for c in eligible if genres
             and not genres.intersection(c.get("genre_ids", []))
             and keyword_score(c, weights) > 0]
    missed = [c for c in cross if c["id"] not in baseline_ids]
    # Popularity truncation opportunity inside matched genres.
    truncated = [c for c in genre_pool if c["id"] not in baseline_ids
                 and keyword_score(c, weights) > 0]
    # Controlled alternative retrieval: 70% baseline, 30% keyword-ranked.
    # Reserve at least one keyword slot when budget > 1; for budget=1,
    # a deterministic baseline-only result avoids a meaningless split.
    keep = budget if budget == 1 else max(1, math.floor(budget * 0.7))
    chosen = ordered(genre_pool)[:keep]
    selected = {c["id"] for c in chosen}
    keyword_candidates = sorted(
        (c for c in eligible if c["id"] not in selected
         and keyword_score(c, weights) > 0),
        key=lambda c: (-keyword_score(c, weights),
                       -int(c["vote_count"]), str(c["id"])))
    chosen.extend(keyword_candidates[:max(0, budget - len(chosen))])
    # Fill unused keyword slots with the remaining eligible baseline titles.
    # Otherwise sparse keyword profiles unfairly shrink the candidate pool.
    selected = {c["id"] for c in chosen}
    if len(chosen) < budget:
        fallback = (c for c in ordered(genre_pool) if c["id"] not in selected)
        for c in fallback:
            chosen.append(c)
            if len(chosen) == budget:
                break
    chosen_ids = {c["id"] for c in chosen}
    # Only diagnostic proxies, not accuracy or relevance metrics.
    top_keywords = sorted(
        (c for c in eligible if keyword_score(c, weights) > 0),
        key=lambda c: (-keyword_score(c, weights),
                       -int(c["vote_count"]), str(c["id"])))[:top_k]
    return {
        "profile_id": profile["id"],
        "eligible_count": len(eligible),
        "genre_eligible_count": len(genre_pool),
        "baseline_pool_count": len(baseline_pool),
        "cross_genre_keyword_matches": len(cross),
        "cross_genre_matches_missing_baseline": len(missed),
        "genre_matches_lost_to_vote_truncation": len(truncated),
        "keyword_first_pool_count": len(chosen),
        "keyword_first_cross_genre_count": sum(
            bool(genres) and not genres.intersection(c.get("genre_ids", []))
            for c in chosen),
        "top_keyword_matches_retrieved_baseline": sum(
            c["id"] in baseline_ids for c in top_keywords),
        "top_keyword_matches_retrieved_alternative": sum(
            c["id"] in chosen_ids for c in top_keywords),
        "top_k": top_k,
        "relevant_top_k_available": len(top_keywords),
    }


def self_test():
    candidates = [
        {"id": "a", "type": "movie", "genre_ids": ["drama"],
         "keyword_ids": ["generic"], "vote_count": 1000,
         "vote_average": 8, "eligible": True},
        {"id": "b", "type": "movie", "genre_ids": ["sci-fi"],
         "keyword_ids": ["time-loop"], "vote_count": 100,
         "vote_average": 8, "eligible": True},
        {"id": "c", "type": "movie", "genre_ids": ["drama"],
         "keyword_ids": ["time-loop"], "vote_count": 50,
         "vote_average": 8, "eligible": True},
        {"id": "d", "type": "movie", "genre_ids": ["sci-fi"],
         "keyword_ids": ["time-loop"], "vote_count": 999,
         "vote_average": 9, "eligible": False},
        {"id": "e", "type": "movie", "genre_ids": ["sci-fi"],
         "keyword_ids": ["time-loop"], "vote_count": 999,
         "vote_average": 9, "eligible": True},
    ]
    profile = {"id": "synthetic", "genre_ids": ["drama"],
               "keyword_weights": {"time-loop": 1}, "excluded_ids": ["e"]}
    result = audit_profile(profile, candidates, budget=2, top_k=2)
    assert result["eligible_count"] == 3, result
    assert result["cross_genre_keyword_matches"] == 1, result
    assert result["cross_genre_matches_missing_baseline"] == 1, result
    assert result["top_keyword_matches_retrieved_alternative"] == 1, result
    assert result["relevant_top_k_available"] == 2, result
    assert keyword_score({"keyword_ids": []}, {"x": 1}) == 0
    empty = audit_profile({"id": "empty", "genre_ids": [],
                           "keyword_weights": {}, "excluded_ids": []},
                          candidates, budget=2, top_k=2)
    assert empty["relevant_top_k_available"] == 0, empty
    assert empty["keyword_first_pool_count"] == 2, empty
    print("Self-test passed (synthetic data only).")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, help="Anonymized staging JSON export")
    parser.add_argument("--budget", type=int, default=500)
    parser.add_argument("--top-k", type=int, default=10)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return
    if not args.input:
        parser.error("Provide --input or --self-test")
    if args.budget < 1 or args.top_k < 1:
        parser.error("--budget and --top-k must be positive")
    data = json.loads(args.input.read_text(encoding="utf-8"))
    results = [audit_profile(p, data["candidates"], args.budget, args.top_k)
               for p in data["profiles"]]
    print(json.dumps({"note": "Retrieval diagnostic only; not actual C# ranking",
                      "profile_count": len(results), "results": results},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
