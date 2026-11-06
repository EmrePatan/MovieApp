"""Unit tests for offline recommendation retrieval diagnostic.

Run: python -m unittest discover -s tools/tests -p 'test_recommendation_retrieval_audit.py'
No external dependencies, network, database or production calls.
"""
import importlib.util
from pathlib import Path
import unittest

MODULE = Path(__file__).resolve().parents[1] / "recommendation_retrieval_audit.py"
spec = importlib.util.spec_from_file_location("recommendation_retrieval_audit", MODULE)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def title(id, genres, keywords, votes, eligible=True):
    return {"id": id, "type": "movie", "genre_ids": genres,
            "keyword_ids": keywords, "vote_count": votes,
            "vote_average": 8.0, "eligible": eligible}


class RetrievalAuditTests(unittest.TestCase):
    def test_cross_genre_keyword_match_is_recovered(self):
        items = [title("popular", ["drama"], ["family"], 900),
                 title("thematic", ["sci-fi"], ["time-loop"], 50)]
        profile = {"id": "p", "genre_ids": ["drama"],
                   "keyword_weights": {"time-loop": 1}, "excluded_ids": []}
        result = audit.audit_profile(profile, items, budget=2)
        self.assertEqual(1, result["cross_genre_matches_missing_baseline"])
        self.assertEqual(1, result["top_keyword_matches_retrieved_alternative"])
        self.assertEqual(0, result["top_keyword_matches_retrieved_baseline"])

    def test_exclusions_and_ineligible_titles(self):
        items = [title("excluded", ["sci-fi"], ["time-loop"], 999),
                 title("unreleased", ["sci-fi"], ["time-loop"], 888, False),
                 title("allowed", ["sci-fi"], ["time-loop"], 100)]
        profile = {"id": "p", "genre_ids": ["drama"],
                   "keyword_weights": {"time-loop": 1},
                   "excluded_ids": ["excluded"]}
        result = audit.audit_profile(profile, items, budget=3)
        self.assertEqual(1, result["eligible_count"])
        self.assertEqual(1, result["keyword_first_pool_count"])

    def test_sparse_keywords_backfill_baseline(self):
        items = [title("a", ["drama"], [], 900),
                 title("b", ["drama"], [], 800),
                 title("c", ["drama"], [], 700)]
        profile = {"id": "p", "genre_ids": ["drama"],
                   "keyword_weights": {}, "excluded_ids": []}
        result = audit.audit_profile(profile, items, budget=3)
        self.assertEqual(3, result["keyword_first_pool_count"])
        self.assertEqual(0, result["relevant_top_k_available"])

    def test_vote_truncation_is_measured(self):
        items = [title("popular", ["drama"], ["generic"], 1000),
                 title("niche", ["drama"], ["time-loop"], 20)]
        profile = {"id": "p", "genre_ids": ["drama"],
                   "keyword_weights": {"time-loop": 1}, "excluded_ids": []}
        result = audit.audit_profile(profile, items, budget=1)
        self.assertEqual(1, result["genre_matches_lost_to_vote_truncation"])

    def test_keyword_score_deduplicates_and_normalizes(self):
        self.assertAlmostEqual(1.0, audit.keyword_score(
            {"keyword_ids": ["x", "x"]}, {"x": 1}))
        self.assertEqual(0, audit.keyword_score(
            {"keyword_ids": []}, {"x": 1}))


if __name__ == "__main__":
    unittest.main()
