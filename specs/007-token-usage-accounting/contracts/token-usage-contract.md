# Contract: Token Usage Accounting

## Canonical fields

Every new canonical result row exposes the following attempt-attributed fields:

| Field | Required column | Nullable | Contract |
|---|---:|---:|---|
| `usage_available` | Yes | No | True only when the observation is complete |
| `usage_status` | Yes | No | One vocabulary value below |
| `usage_source` | Yes | Yes | Required for complete or partial observations |
| `usage_policy_version` | Yes | Yes | Required for new complete or partial observations |
| `input_tokens` | Yes | Yes | Non-negative per-attempt input component |
| `output_tokens` | Yes | Yes | Non-negative per-attempt output component |
| `estimated_prompt_tokens` | Yes | Yes | Agent-tool diagnostic only |
| `total_tokens` | Yes | Yes | `input_tokens + output_tokens` when complete |
| `cumulative_input_tokens` | Yes | Yes | Chain-aware input component |
| `cumulative_output_tokens` | Yes | Yes | Chain-aware output component |
| `cumulative_tokens` | Yes | Yes | Sum of cumulative components when both exist |

Child result rows may repeat attempt-attributed token fields, as they do today. Consumers must
deduplicate or aggregate at attempt/candidate grain and must not sum repeated child-row values.

## Status contract

| Status | `usage_available` | Components | Total |
|---|---:|---|---|
| `complete-reported` | true | both required | derived and required |
| `complete-estimated` | true | both required | derived and required |
| `partial` | false | at least one known | null |
| `missing` | false | none for new rows | null |
| `not-applicable` | false | none | null |

## Built-in lane contract

- Source: `cl100k-local-estimate`.
- Policy: `token-accounting-v1`.
- Chat input counts system-message content and user-message content separately.
- Inference input counts prompt content.
- Output counts returned response text.
- Counts are estimates, not provider billing or provider-reported usage.
- Provider framing, hidden context, cached-input detail, and hidden reasoning are outside the estimate.
- Each retry invocation contributes input once. Each returned response contributes output once.
- An invocation with unobservable output prevents complete status for the logical step/attempt.

## Agent-tool normalization contract

| Tool | Preferred source | Input rule | Output rule | Record aggregation |
|---|---|---|---|---|
| Codex | `turn.completed.usage` | aggregate `input_tokens`; cached detail is not added | aggregate `output_tokens`; reasoning detail is not added | latest cumulative completion |
| Claude | final result usage | input + cache creation + cache read | output | final cumulative result |
| Gemini | result stats | aggregate input/prompt | aggregate total minus input, otherwise output categories | final result; support stream JSON and JSON file |
| OpenHands | persisted accumulated usage | preferred prompt/input alias + disjoint cache read/write | preferred completion/output alias + disjoint reasoning | sum independent metric owners |
| mini-swe-agent | trajectory response usage | sum per-call prompt/input | sum per-call completion/output | trajectory calls only |
| Aider | stdout token footer | sent | received | latest cumulative footer |
| Copilot | OTEL metric, spans, stderr | aggregate input; cache detail only when aggregate absent | aggregate output; reasoning detail only when aggregate absent | latest cumulative metric, else sum spans, else latest footer |

## Repair-chain contract

- Per-attempt components always describe that attempt only.
- Cumulative components describe the initial attempt through the current repair.
- A component becomes unavailable from the first repair-chain attempt lacking that component.
- Cumulative total exists only when both cumulative components exist.
- Candidate-level built-in cost uses the terminal cumulative components once.
- Independent attempts use their own values as cumulative values.
- Agent-tool attempts remain self-contained and use their own reported totals unless a future explicit
  repair relationship is introduced.

## Historical contract

- A pre-policy built-in `total_tokens_used` or step `tokens_used` value is a legacy prompt-only
  estimate. It is not valid complete total usage and must not be split or promoted automatically.
- Existing agent-tool input/output values remain valid according to their retained source. A total may
  be derived when both components are present.
- Analyses must retain usage status/source/policy and may compare reported and estimated populations
  only with an explicit methodological choice.

## Integrity rules

Cost analysis must reject or quarantine rows where:

- a complete status lacks either component;
- total differs from input plus output;
- a partial status has no known component;
- missing/not-applicable new-policy rows contain canonical components;
- a complete/partial new-policy row lacks source or policy;
- any count is negative or silently saturated;
- cumulative total differs from cumulative input plus cumulative output;
- a repair cumulative component decreases or omits a known preceding attempt;
- child-row repetition is summed as independent attempts.
