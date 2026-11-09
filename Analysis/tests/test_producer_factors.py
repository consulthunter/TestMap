import pandas as pd

from analysis.normalize import compute_model_family, compute_tool_family


def _attempts(rows):
    return pd.DataFrame(rows, columns=["lane", "tool_id", "model"])


def test_tool_family_strips_the_backbone_suffix():
    df = _attempts([
        ("agentic", "openhands-custom-gpt", "gpt-oss-120b"),
        ("agentic", "openhands-gemini", "gemini-3.6-flash"),
        ("agentic", "mini-swe-agent-custom-glm", "GLM-5.3"),
        ("agentic", "copilot-anthropic", "claude-haiku-4.5"),
    ])
    assert list(compute_tool_family(df)) == ["openhands", "openhands", "mini-swe-agent", "copilot"]


def test_tool_family_keeps_single_model_tools_and_labels_the_llm_lane():
    df = _attempts([
        ("agentic", "codex", "gpt-5.6-luna"),
        ("agentic", "claude", "claude-haiku-4.5"),
        ("agentic", "gemini", "gemini-3.6-flash"),
        ("llm", None, "gpt-oss-120b"),
    ])
    assert list(compute_tool_family(df)) == ["codex", "claude", "gemini", "llm (direct)"]


def test_model_family_aligns_the_lanes_on_one_backbone():
    # The LLM arm and the tool ran the same backbone under different names.
    df = _attempts([
        ("llm", None, "GLM-5.3-thinking-high"),
        ("agentic", "openhands-custom-glm", "GLM-5.3"),
        ("llm", None, "us.anthropic.claude-haiku-4-5-20251001-v1:0"),
        ("agentic", "copilot-anthropic", "claude-haiku-4.5"),
        ("llm", None, "Kimi-K3-thinking-high"),
    ])
    families = list(compute_model_family(df))
    assert families[0] == families[1] == "glm-5.3"
    assert families[2] == families[3] == "claude-haiku-4.5"
    assert families[4] == "kimi-k3"


def test_model_family_is_missing_when_the_model_is():
    df = _attempts([("agentic", "codex", None), ("llm", None, "")])
    assert compute_model_family(df).isna().all()


def test_final_chain_outcomes_collapses_repair_steps_to_the_final_outcome():
    from analysis.normalize import final_chain_outcomes

    rows = pd.DataFrame([
        # Repair chain: fails twice, then succeeds.
        dict(candidate_key="c1", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=1, outcome_classification="ValidationFailed", infrastructure_failure=False,
             duration_seconds=60.0),
        dict(candidate_key="c1", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=2, outcome_classification="ValidationFailed", infrastructure_failure=False,
             duration_seconds=30.0),
        dict(candidate_key="c1", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=3, outcome_classification="ValidatedEvidencePositive", infrastructure_failure=False,
             duration_seconds=90.0),
        # Repair chain whose last step is an infrastructure failure: ends at step 1.
        dict(candidate_key="c2", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=1, outcome_classification="ValidationFailed", infrastructure_failure=False),
        dict(candidate_key="c2", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=2, outcome_classification="ValidationFailed", infrastructure_failure=True),
        # Chain lost entirely to infrastructure: missing, no row.
        dict(candidate_key="c3", lane="llm", producer="m", budget_mode="PassAt1",
             attempt_number=1, outcome_classification="ValidationFailed", infrastructure_failure=True),
        # Single-attempt agentic chain: unchanged.
        dict(candidate_key="c1", lane="agentic", producer="t", budget_mode="PassAt1",
             attempt_number=1, outcome_classification="NoChange", infrastructure_failure=False),
    ])

    final = final_chain_outcomes(rows).set_index(["candidate_key", "lane"])

    assert len(final) == 3
    assert final.loc[("c1", "llm"), "outcome_classification"] == "ValidatedEvidencePositive"
    assert final.loc[("c1", "llm"), "chain_attempts"] == 3
    assert final.loc[("c1", "llm"), "chain_duration_seconds"] == 180.0
    assert final.loc[("c2", "llm"), "outcome_classification"] == "ValidationFailed"
    assert final.loc[("c2", "llm"), "chain_attempts"] == 1
    assert final.loc[("c1", "agentic"), "outcome_classification"] == "NoChange"
    assert ("c3", "llm") not in final.index


def test_final_chain_outcomes_is_idempotent():
    from analysis.normalize import final_chain_outcomes

    rows = pd.DataFrame([
        dict(candidate_key="c1", lane="llm", producer="m", budget_mode="PassAt1RepairAt5",
             attempt_number=n, outcome_classification=o, infrastructure_failure=False, duration_seconds=d)
        for n, o, d in [(1, "ValidationFailed", 60.0), (2, "ValidatedEvidencePositive", 40.0)]
    ])
    once = final_chain_outcomes(rows)
    twice = final_chain_outcomes(once)
    assert twice["chain_duration_seconds"].tolist() == once["chain_duration_seconds"].tolist() == [100.0]
