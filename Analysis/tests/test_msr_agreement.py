"""Tests for the inter-rater agreement statistics and the rating form parser."""

from pathlib import Path

import pytest

from analysis.msr_agreement import (
    cohens_kappa,
    krippendorff_alpha_nominal,
    parse_form,
    wilson_interval,
)


class TestCohensKappa:
    def test_matches_hand_computed_value(self):
        # observed = 4/5 = 0.80
        # expected = (2/5)(1/5) + (3/5)(4/5) = 0.56
        # kappa = (0.80 - 0.56) / (1 - 0.56) = 0.5454...
        a = ["yes", "yes", "no", "no", "no"]
        b = ["yes", "no", "no", "no", "no"]
        assert cohens_kappa(a, b) == pytest.approx(0.545454, abs=1e-5)

    def test_perfect_agreement_is_one(self):
        a = ["x", "y", "x", "y"]
        assert cohens_kappa(a, list(a)) == pytest.approx(1.0)

    def test_chance_level_agreement_is_zero(self):
        a = ["a", "a", "b", "b"]
        b = ["a", "b", "a", "b"]
        assert cohens_kappa(a, b) == pytest.approx(0.0, abs=1e-12)

    def test_undefined_when_one_category_used_throughout(self):
        # Both raters answered "correct" for every unit: agreement is total, but
        # there is no variance to correct for, so kappa is undefined rather than 1.
        assert cohens_kappa(["correct"] * 5, ["correct"] * 5) is None

    def test_returns_none_for_empty_or_mismatched_input(self):
        assert cohens_kappa([], []) is None
        assert cohens_kappa(["a"], ["a", "b"]) is None


class TestKrippendorffAlpha:
    def test_perfect_agreement_is_one(self):
        units = [["a", "a"], ["b", "b"], ["a", "a"], ["b", "b"]]
        assert krippendorff_alpha_nominal(units) == pytest.approx(1.0)

    def test_total_disagreement_is_negative(self):
        units = [["a", "b"], ["b", "a"], ["a", "b"], ["b", "a"]]
        assert krippendorff_alpha_nominal(units) < 0

    def test_ignores_units_with_a_single_rating(self):
        paired = [["a", "a"], ["b", "b"]]
        with_singletons = paired + [["a"], ["b"]]
        assert krippendorff_alpha_nominal(with_singletons) == pytest.approx(
            krippendorff_alpha_nominal(paired)
        )

    def test_returns_none_without_pairable_units(self):
        assert krippendorff_alpha_nominal([["a"], ["b"]]) is None
        assert krippendorff_alpha_nominal([]) is None


class TestWilsonInterval:
    def test_matches_published_value(self):
        low, high = wilson_interval(5, 10)
        assert low == pytest.approx(0.2366, abs=1e-4)
        assert high == pytest.approx(0.7634, abs=1e-4)

    def test_bounds_stay_inside_zero_and_one(self):
        assert wilson_interval(0, 10)[0] == 0.0
        assert wilson_interval(10, 10)[1] == 1.0

    def test_no_trials_gives_nan(self):
        low, high = wilson_interval(0, 0)
        assert low != low and high != high  # NaN


def _write_form(tmp_path: Path, rater: str, sheet: str, unit_id: str, block: str) -> Path:
    directory = tmp_path / "ratings" / rater / sheet
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{unit_id}.md"
    path.write_text(f"# {sheet} - {unit_id}\n\n```yaml\n{block}\n```\n", encoding="utf-8")
    return path


class TestParseForm:
    def test_reads_a_single_label_answer(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "metrics_attribution", "metrics_attribution-0001",
            "unit_id: metrics_attribution-0001\nrater: rater_a\n"
            "label: correct\nconfidence: high\nnotes: \"\"",
        )
        record = parse_form(path)
        assert record["status"] == "ok"
        assert record["label"] == "correct"
        assert record["rater"] == "rater_a"
        assert record["sheet"] == "metrics_attribution"

    def test_empty_label_list_is_a_real_answer(self, tmp_path):
        # "no smells present" must not be mistaken for an unfilled form.
        path = _write_form(
            tmp_path, "rater_a", "smells_correctness", "smells_correctness-0001",
            "unit_id: smells_correctness-0001\nrater: rater_a\n"
            "labels: []\nconfidence: high\nnotes: \"\"",
        )
        record = parse_form(path)
        assert record["status"] == "ok"
        assert record["labels"] == []

    def test_missing_confidence_marks_multi_label_form_unfilled(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "smells_correctness", "smells_correctness-0002",
            "unit_id: smells_correctness-0002\nrater: rater_a\nlabels: []\nconfidence:",
        )
        assert parse_form(path)["status"] == "unfilled"

    def test_blank_single_label_is_unfilled(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "mapping_exercise", "mapping_exercise-0003",
            "unit_id: mapping_exercise-0003\nrater: rater_a\nlabel:\nconfidence:",
        )
        assert parse_form(path)["status"] == "unfilled"

    def test_missing_yaml_block_is_malformed(self, tmp_path):
        directory = tmp_path / "ratings" / "rater_a" / "mapping_exercise"
        directory.mkdir(parents=True)
        path = directory / "mapping_exercise-0004.md"
        path.write_text("# no block here\n", encoding="utf-8")
        record = parse_form(path)
        assert record["status"] == "malformed"
        assert "no fenced yaml block" in record["problem"]

    def test_unit_id_must_match_the_filename(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "mapping_exercise", "mapping_exercise-0005",
            "unit_id: mapping_exercise-9999\nrater: rater_a\nlabel: exercised",
        )
        record = parse_form(path)
        assert record["status"] == "malformed"
        assert "does not match filename" in record["problem"]

    def test_rater_must_match_the_directory(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "mapping_exercise", "mapping_exercise-0006",
            "unit_id: mapping_exercise-0006\nrater: rater_b\nlabel: exercised",
        )
        record = parse_form(path)
        assert record["status"] == "malformed"
        assert "does not match directory" in record["problem"]

    def test_labels_must_be_a_list(self, tmp_path):
        path = _write_form(
            tmp_path, "rater_a", "smells_correctness", "smells_correctness-0007",
            "unit_id: smells_correctness-0007\nrater: rater_a\nlabels: Eager Test",
        )
        record = parse_form(path)
        assert record["status"] == "malformed"
        assert "labels is not a list" in record["problem"]
