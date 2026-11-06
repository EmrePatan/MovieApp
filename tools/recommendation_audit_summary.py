#!/usr/bin/env python3
"""Summarize offline retrieval audit JSON without leaking profile identifiers.

Usage:
  python tools/recommendation_audit_summary.py --input audit-output.json
Read-only. Requires no dependencies. Reports retrieval proxies, not NDCG.
"""
import argparse
import json
import statistics
from pathlib import Path

METRICS = (
    "eligible_count",
    "genre_eligible_count",
    "baseline_pool_count",
    "cross_genre_keyword_matches",
    "cross_genre_matches_missing_baseline",
    "genre_matches_lost_to_vote_truncation",
    "keyword_first_pool_count",
    "keyword_first_cross_genre_count",
    "relevant_top_k_available",
    "top_keyword_matches_retrieved_baseline",
    "top_keyword_matches_retrieved_alternative",
)


def summarize(data):
    rows = data.get("results", [])
    if not rows:
        return {"profile_count": 0, "warning": "No profiles; cannot compare."}
    for row in rows:
        for metric in METRICS:
            if metric not in row or not isinstance(row[metric], (int, float)):
                raise ValueError(f"Missing or nonnumeric metric: {metric}")
    comparable = [row for row in rows if row["relevant_top_k_available"] > 0]
    report = {
        "profile_count": len(rows),
        "profiles_with_keyword_opportunity": len(comparable),
        "mean_per_profile": {
            metric: round(statistics.mean(row[metric] for row in rows), 3)
            for metric in METRICS
        },
        "keyword_top_k_retrieval_rate": {
            "baseline": (
                round(statistics.mean(
                    row["top_keyword_matches_retrieved_baseline"] /
                    row["relevant_top_k_available"] for row in comparable), 4)
                if comparable else None
            ),
            "alternative": (
                round(statistics.mean(
                    row["top_keyword_matches_retrieved_alternative"] /
                    row["relevant_top_k_available"] for row in comparable), 4)
                if comparable else None
            ),
        },
        "limitations": (
            "Keyword-overlap retrieval proxy only; not actual production candidate "
            "selection, ranking, NDCG, Recall, satisfaction or performance."
        ),
    }
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(args.input.read_text(encoding="utf-8"))
    print(json.dumps(summarize(data), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
