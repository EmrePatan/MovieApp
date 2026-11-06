#!/usr/bin/env python3
"""Create deterministic synthetic taste profiles from a catalog-only export.

No user activity or identifiers are accessed. These profiles exercise the
pipeline; they CANNOT establish recommendation relevance or user satisfaction.
"""
import argparse
import json
from collections import Counter
from pathlib import Path


def make_profiles(candidates, limit=12):
    usable = [c for c in candidates if c.get("eligible") is True
              and c.get("keyword_ids") and c.get("genre_ids")]
    usable.sort(key=lambda c: str(c["id"]))
    # Spread seeds across the catalog rather than taking only popular titles.
    if not usable:
        return []
    count = min(limit, len(usable))
    indices = [i * len(usable) // count for i in range(count)]
    profiles = []
    for i, index in enumerate(indices):
        seed = usable[index]
        keywords = sorted(set(seed["keyword_ids"]))
        genres = sorted(set(seed["genre_ids"]))
        profiles.append({
            "id": f"synthetic-{i+1:02d}",
            "genre_ids": genres,
            "keyword_weights": {key: 1.0 for key in keywords},
            # Exclude the source item to prevent self-match leakage.
            "excluded_ids": [seed["id"]],
        })
    return profiles


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--profiles", type=int, default=12)
    args = parser.parse_args()
    if args.profiles < 1:
        parser.error("--profiles must be positive")
    data = json.loads(args.input.read_text(encoding="utf-8"))
    if data.get("profiles"):
        parser.error("Refusing to overwrite a file containing existing profiles")
    data["profiles"] = make_profiles(data["candidates"], args.profiles)
    if not data["profiles"]:
        parser.error("No eligible catalog titles with genres and keywords")
    args.output.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    print(f"Generated {len(data['profiles'])} SYNTHETIC profiles; not real user evaluation.")


if __name__ == "__main__":
    main()
