# Data Model: Token Usage Accounting

## Shared vocabulary

### Usage status

| Value | Meaning | Required token fields |
|---|---|---|
| `complete-reported` | Both components came from retained provider/tool evidence | input, output, derived total |
| `complete-estimated` | Both components were locally estimated under the named policy | input, output, derived total |
| `partial` | At least one known component exists, but complete cost is unavailable | any known component; total null |
| `missing` | The operation was applicable but no defensible component is available | input/output/total null for new rows |
| `not-applicable` | No provider or tool invocation occurred at this grain | input/output/total null |

### Accounting policy

`token-accounting-v1` defines:

- Built-in input: SharpToken `cl100k_base` count of each TestMap-supplied message content segment.
- Built-in output: SharpToken `cl100k_base` count of returned response text.
- Chat system and user content are counted; provider framing and hidden context are not.
- Complete total is input plus output.
- Retry invocation components are accumulated inside the logical generation step.
- Tool-reported cache/reasoning/detail fields are folded according to the tool rules in the contract.
- Aggregate fields take precedence over equivalent detail/alias fields.

Historical rows without this policy are not complete under `token-accounting-v1`.

## Generation step

**Grain**: One logical step in one generation attempt.

| Field | Type | Rule |
|---|---|---|
| `InputTokens` | nullable integer | Sum of known request estimates for provider invocations in the step |
| `OutputTokens` | nullable integer | Sum of known response estimates for provider invocations in the step |
| `TokenCount` / `tokens_used` | nullable integer | Canonical total for new-policy rows; equals input plus output only when complete |
| `UsageStatus` | required vocabulary value | `not-applicable` for local/skipped/fallback steps |
| `UsageSource` | string | `cl100k-local-estimate`, aggregate source, or legacy source |
| `UsagePolicyVersion` | string | `token-accounting-v1` for new observations |

**Validation rules**:

- Complete statuses require non-negative input/output and `TokenCount = InputTokens + OutputTokens`.
- Partial requires at least one known component and null `TokenCount`.
- Missing and not-applicable require null canonical components/total for new rows.
- Existing legacy `tokens_used` values may remain non-null only with legacy source/policy and are not
  accepted as complete split usage.

## Generation attempt

**Grain**: One initial or repair attempt for one candidate.

| Field | Type | Rule |
|---|---|---|
| `InputTokens` | nullable integer | Sum across applicable steps only when each required input component is known |
| `OutputTokens` | nullable integer | Sum across applicable steps only when each required output component is known |
| `TotalTokensUsed` | nullable integer | Derived from complete attempt input/output |
| `UsageStatus` | required vocabulary value | Reduced from applicable child step statuses |
| `UsageSource` | string | Shared estimate source or `generation_steps` for a mixed aggregate |
| `UsagePolicyVersion` | string | Effective policy for the attempt |
| `ChainCumulativeInputTokens` | transient nullable integer | Running input sum through current repair |
| `ChainCumulativeOutputTokens` | transient nullable integer | Running output sum through current repair |
| `ChainCumulativeTokensUsed` | transient nullable integer | Derived from cumulative input/output |

**Relationships**:

- A generation attempt owns zero or more generation steps.
- A repair attempt links to its parent through existing parent attempt identity.
- Cumulative fields are reconstructed in attempt order and exported; they are not separately stored.

**Status reduction**:

- No applicable steps: `not-applicable`.
- Every applicable step complete-estimated: `complete-estimated`.
- Every applicable step complete-reported: `complete-reported` (reserved for future built-in policy).
- Any known component with an incomplete required component: `partial`.
- Otherwise: `missing`.

## Agent-tool attempt

**Grain**: One complete invocation of one configured agent tool for one candidate.

| Field | Type | Rule |
|---|---|---|
| `InputTokens` | nullable integer | Existing normalized artifact-derived input |
| `OutputTokens` | nullable integer | Existing normalized artifact-derived output |
| `TotalTokens` | derived nullable integer | Input plus output when both exist; not persisted |
| `EstimatedPromptTokens` | nullable integer | Existing diagnostic estimate; never substituted for input |
| `UsageAvailable` | boolean compatibility field | True only for `complete-reported` |
| `UsageStatus` | required vocabulary value | Reported completeness state |
| `UsageSource` | string | Existing artifact/record source |
| `UsagePolicyVersion` | string | `token-accounting-v1` for newly normalized attempts |

**Validation rules**:

- Complete-reported requires both components and a non-empty source.
- Partial requires exactly one component or a known incomplete aggregate.
- Missing has no reported component.
- `EstimatedPromptTokens` does not affect status or total.

## Canonical result row

**Grain**: Existing attempt/generated-test/test-result row grain; token usage remains attributable to
the parent attempt when repeated on child rows.

| Field | Type | Meaning |
|---|---|---|
| `input_tokens` | nullable integer | Per-attempt input |
| `output_tokens` | nullable integer | Per-attempt output |
| `total_tokens` | nullable integer | Per-attempt derived total |
| `cumulative_input_tokens` | nullable integer | Repair-chain input through this attempt; own input for independent attempt |
| `cumulative_output_tokens` | nullable integer | Repair-chain output through this attempt; own output for independent attempt |
| `cumulative_tokens` | nullable integer | Derived cumulative total |
| `usage_status` | vocabulary string | Measurement completeness and reported/estimated quality |
| `usage_source` | string | Origin of the values |
| `usage_policy_version` | string | Normalization/aggregation policy |
| `usage_available` | boolean compatibility field | True only for complete-reported or complete-estimated |

## Repair cumulative state transitions

For each component independently:

1. Initial state is available with value zero before the first attempt.
2. A known attempt component adds to the running component while it remains available.
3. A missing attempt component changes that cumulative component to unavailable for the current and
   later attempts in the chain.
4. Cumulative total exists only when cumulative input and output both exist.
5. Independent attempts start a new cumulative state and therefore equal their own components.

## Historical compatibility

- Existing generation steps/attempts have no new usage policy. Their stored token total is a legacy
  prompt-only estimate and does not establish complete input/output usage.
- Migration does not synthesize output or split a legacy total.
- Existing tool attempts with both components can derive total and complete-reported status under the
  legacy parser source; partial and missing rows retain their nulls.
- Raw prompts, responses, tool artifacts, parent links, and existing result files remain unchanged.
