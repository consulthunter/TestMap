# Feature Specification: Token Usage Accounting

**Feature Branch**: `007-token-usage-accounting`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "Capture input and output token usage for every evaluation lane, derive total usage, identify reported versus estimated measurements, preserve repair-chain cumulative usage, and make the split available to downstream analysis."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trustworthy Per-Attempt Token Usage (Priority: P1)

As a researcher, I can see the input, output, and total token usage for each generation or tool attempt, together with whether the measurement was reported, estimated, partial, missing, or not applicable, so that cost comparisons do not silently mix incompatible measurements.

**Why this priority**: Per-attempt token usage is the base observation used by all later repair-chain and cross-lane cost comparisons. Incorrect or ambiguous values invalidate those comparisons.

**Independent Test**: Run one built-in generation attempt and one agent-tool attempt with known usage evidence, then verify that each attempt and its exported result retain the expected input, output, total, classification, source, and policy identity.

**Acceptance Scenarios**:

1. **Given** a built-in generation call whose provider usage is unavailable, **When** the call returns a response, **Then** the actual request and response are tokenized separately, the system prompt and user request are included in input usage, total equals input plus output, and the usage is classified as `complete-estimated` with the configured tokenizer as its source.
2. **Given** an agent tool that reports both input and output usage, **When** its attempt is collected, **Then** the reported components are retained, total equals their sum, and the usage is classified as `complete-reported` with the originating artifact identified.
3. **Given** only one token component is available, **When** the attempt is persisted and exported, **Then** the available component is retained, the unavailable component and total remain unavailable, and usage is classified as `partial`.
4. **Given** no token evidence is available for an applicable provider or tool call, **When** the attempt is persisted and exported, **Then** all token values remain unavailable and usage is classified as `missing` rather than zero.
5. **Given** a pipeline step that performs no provider or tool call, **When** the attempt is aggregated, **Then** the step is classified as `not-applicable` and does not make otherwise complete attempt usage incomplete.

---

### User Story 2 - Complete Repair-Chain Cost (Priority: P1)

As a researcher, I can distinguish the cost of an individual generation or repair attempt from the cumulative cost required to reach that point in a repair chain, including separate input and output totals.

**Why this priority**: Repair outcomes cannot be compared fairly with independent attempts or agent-tool runs unless the complete cost of the initial attempt and every preceding repair is represented exactly once.

**Independent Test**: Execute or simulate an initial generation followed by multiple repairs with known component usage and verify each attempt's own usage and the running cumulative input, output, and total values.

**Acceptance Scenarios**:

1. **Given** an initial generation followed by two repair attempts, **When** each result is recorded, **Then** every attempt retains its own input, output, and total usage and also records cumulative input, output, and total usage from the initial attempt through that repair.
2. **Given** a repair chain with complete usage for every attempt, **When** cumulative values are calculated, **Then** cumulative total equals cumulative input plus cumulative output and each cumulative component equals the sum of that component across the chain to that point.
3. **Given** a repair chain containing a partial or missing applicable measurement, **When** later cumulative values are calculated, **Then** known components remain available where valid, affected cumulative totals remain unavailable, and the chain is not presented as complete.
4. **Given** an independent non-repair attempt, **When** cumulative usage is reported, **Then** its cumulative values equal its own per-attempt values.
5. **Given** candidate-level analysis of a repair chain, **When** total cost is summarized, **Then** the terminal cumulative cost is used once rather than summing running cumulative values across repair attempts.

---

### User Story 3 - Comparable Lane and Tool Analysis (Priority: P2)

As an analyst, I can summarize input, output, and total token usage separately by lane, provider, model, tool, candidate, and repair chain while retaining measurement quality and source.

**Why this priority**: The split only delivers research value when downstream datasets and summaries preserve it and prevent reported and estimated measurements from being interpreted as identical evidence.

**Independent Test**: Build an evaluation dataset from fixtures containing reported, estimated, partial, missing, not-applicable, independent, and repair-chain observations and verify the component summaries, missingness audit, and chain-aware totals.

**Acceptance Scenarios**:

1. **Given** a mixed-lane result set, **When** it is normalized, **Then** input, output, total, cumulative input, cumulative output, cumulative total, usage classification, source, and accounting policy remain available for analysis.
2. **Given** reported agent-tool usage and estimated built-in-lane usage, **When** summaries are produced, **Then** measurements can be grouped or filtered by classification and are not silently treated as the same measurement quality.
3. **Given** any complete usage observation, **When** data integrity is audited, **Then** the audit verifies that total equals input plus output and identifies violations.
4. **Given** partial or missing applicable usage, **When** data completeness is summarized, **Then** the missing component and its classification are counted explicitly.

### Edge Cases

- A provider call is retried after an empty response or failure; all observable invocation usage contributes to the logical step, and any unobservable billed component prevents the affected aggregate from being classified as complete.
- A returned response is empty but usage evidence exists; usage remains a valid cost observation even though generation failed.
- A tool emits multiple usage records; the accounting policy must declare whether records are cumulative snapshots or per-call increments before selecting the latest record or summing them.
- A tool reports cached-input, reasoning-output, thought, or tool-use token details; these details are normalized consistently into top-level input or output according to the named policy without being double-counted.
- A tool emits both aggregate fields and component aliases; aggregate values take precedence so equivalent fields are not added twice.
- Token counts exceed the supported numeric range; the observation fails visibly or remains unavailable rather than being silently truncated.
- A generated attempt contains executed provider steps together with local, skipped, or fallback steps; only provider-applicable steps participate in token completeness and aggregation.
- A historical row contains only the legacy prompt count; it is not reclassified as complete input/output usage and is not silently used as exact total usage.
- A repair chain changes context size between attempts; each attempt is measured from its actual request rather than inferred from the initial attempt.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST represent token usage with nullable input, output, and total values at each lane's natural attempt grain.
- **FR-002**: The system MUST represent token usage for each provider-applicable built-in generation step so an attempt can be reconstructed from its constituent steps.
- **FR-003**: Complete total usage MUST be derived from input plus output and MUST NOT be accepted as an independently contradictory value.
- **FR-004**: Total usage MUST remain unavailable when either required component is unavailable.
- **FR-005**: Built-in generation usage MUST be estimated by separately tokenizing the actual request and returned response when provider-reported usage is unavailable.
- **FR-006**: Built-in estimated input usage MUST include every message submitted by TestMap for the call, including the system instruction and user request.
- **FR-007**: Built-in locally tokenized usage MUST be classified as `complete-estimated` and identify the tokenizer and accounting policy used; it MUST NOT be represented as provider-reported usage.
- **FR-008**: Agent-tool usage obtained from tool artifacts or telemetry MUST be classified as `complete-reported` only when both input and output components are available.
- **FR-009**: Every applicable usage observation MUST have exactly one classification from `complete-reported`, `complete-estimated`, `partial`, or `missing`; non-provider steps MUST use `not-applicable`.
- **FR-010**: Every non-missing usage observation MUST retain a source sufficient to identify how the value was obtained.
- **FR-011**: Every usage observation MUST retain a versioned accounting policy that defines component normalization, aggregation, cached-token treatment, reasoning-token treatment, retry handling, and total derivation.
- **FR-012**: Local, skipped, and fallback steps that make no provider call MUST be excluded from attempt usage completeness and summation.
- **FR-013**: Multiple observable provider invocations caused by retries MUST contribute to the logical step's usage exactly once per invocation.
- **FR-014**: A failed or empty generation result MUST retain any observable usage even when the generation outcome itself is unsuccessful.
- **FR-015**: A built-in generation attempt MUST aggregate input and output across its applicable steps and derive its per-attempt total from those aggregates.
- **FR-016**: An agent-tool attempt MUST expose input, output, and derived total usage at the attempt grain without substituting its prompt estimate for reported input usage.
- **FR-017**: For an independent attempt, cumulative input, output, and total MUST equal that attempt's corresponding values.
- **FR-018**: For a repair attempt, cumulative input and output MUST equal the component sums from the initial attempt through the current repair, and cumulative total MUST be derived from the cumulative components.
- **FR-019**: If any applicable measurement needed for a cumulative component is unavailable, the affected cumulative component and cumulative total MUST remain unavailable rather than treating the missing value as zero.
- **FR-020**: Candidate-level repair cost MUST use the terminal cumulative values once; it MUST NOT sum intermediate cumulative values.
- **FR-021**: Canonical result exports MUST include per-attempt input, output, total, cumulative input, cumulative output, cumulative total, usage classification, source, and accounting policy.
- **FR-022**: Downstream normalized datasets and summaries MUST retain and separately aggregate input, output, and total usage while preserving usage classification and source.
- **FR-023**: Data audits MUST identify missing applicable components, contradictory totals, invalid classifications, absent sources, and incomplete repair-chain cumulative values.
- **FR-024**: Historical prompt-only token counts MUST remain identifiable as legacy estimates and MUST NOT be automatically promoted to complete input/output observations.
- **FR-025**: Existing reported agent-tool input and output values MUST remain compatible; complete historical tool rows MAY derive total as input plus output without rerunning the tool.
- **FR-026**: The initial accounting policy MUST cover Codex, Claude, Gemini, OpenHands, mini-swe-agent, Aider, and Copilot artifact formats and MUST prevent aggregate/detail double-counting.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Provide honest, reproducible cost evidence for built-in generation and agent-tool comparisons, including the full cost of repair chains. The feature prevents prompt-only estimates, missing output usage, and cumulative double-counting from distorting cost claims.
- **Unit of analysis**: Provider-applicable generation step for built-in measurement; generation attempt or agent-tool attempt for lane comparison; ordered repair chain for cumulative cost; candidate for terminal chain cost; run and repository only through explicit aggregation.
- **Sampling and eligibility**: Token accounting does not change candidate sampling or experiment eligibility. Every attempted provider or agent-tool invocation is eligible for cost measurement regardless of generation, compilation, validation, or impact outcome. Non-provider steps are not applicable rather than excluded observations.
- **Comparison contract**: Both lanes expose the same top-level input, output, and total fields, but retain whether values were reported or estimated. Repair attempts retain their individual cost and running chain cost. Agent-tool runs remain self-contained unless an explicit repair relationship is introduced later.
- **Measurement contract**: Input means tokens attributable to submitted model context under the named policy; output means returned completion plus policy-designated output categories; total is input plus output. Built-in-lane measurements are estimated through the configured tokenizer when provider usage is unavailable. Tool-specific cached, reasoning, thought, and tool-use details are folded according to a versioned common policy.
- **Missingness and failures**: Null represents unavailable, never zero. Applicable usage is classified as complete-reported, complete-estimated, partial, or missing. Steps with no model call are not-applicable. Failed, timed-out, empty, and retried calls retain all observable cost evidence and remain separate from generation success.
- **Provenance contract**: Every usage observation retains the existing repository, commit, experiment, candidate, attempt, lane, provider/model or tool identity, plus usage classification, usage source, and accounting-policy version. Existing raw tool artifacts and generation prompts/responses remain the audit evidence.
- **Compatibility**: Existing component fields and canonical export fields are retained. New attempt-level component and cumulative component values are additive. Legacy built-in totals remain legacy prompt estimates unless they can be explicitly reconstructed and labelled; existing complete agent-tool components may safely derive totals. Any changed interpretation is gated by the accounting-policy version and compatible result-schema handling.
- **Audit criteria**: Analysis warns on missing or partial applicable usage, absent sources, and legacy-only estimates. Contradictory totals, negative values, invalid classifications, silent numeric truncation, double-counted repair costs, or complete classifications with missing components are integrity failures that block cost comparisons until resolved.

### Key Entities *(include if feature involves data)*

- **Token Usage Observation**: Usage measured for one provider-applicable step or one agent-tool attempt, containing nullable input, output, derived total, classification, source, and accounting-policy version.
- **Generation Step**: One logical stage in built-in generation. It may contain one or more provider invocations because of retries, or may be not applicable to token usage when completed locally.
- **Generation Attempt**: One initial or repair generation attempt whose component usage is aggregated from applicable generation steps.
- **Agent-Tool Attempt**: One self-contained tool invocation whose usage is normalized from retained tool artifacts or telemetry.
- **Repair Chain Usage**: Ordered cumulative input, output, and total cost from the initial generation through a selected repair attempt.
- **Accounting Policy**: Versioned rules defining applicability, estimation, reported-source normalization, cached/reasoning token treatment, retry aggregation, completeness, and total derivation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of new generation and agent-tool attempts receive an explicit usage classification, including attempts with no available usage.
- **SC-002**: 100% of complete observations satisfy `total = input + output`; no complete observation contains a missing component.
- **SC-003**: 100% of built-in locally tokenized observations are identifiable as estimated and name their tokenizer source; none are labelled provider-reported.
- **SC-004**: For deterministic repair-chain fixtures, every cumulative input, output, and total value matches the expected running sum, and candidate-level cost counts the terminal cumulative value exactly once.
- **SC-005**: For deterministic fixtures covering all seven supported agent tools, normalized input/output values match the retained artifact evidence and aggregate/detail fields are not double-counted.
- **SC-006**: Normalized evaluation data and cost summaries expose separate input, output, and total measures with zero loss of usage classification, source, or accounting-policy identity.
- **SC-007**: Integrity audits identify 100% of fixture cases containing contradictory totals, incomplete complete-classifications, missing applicable usage, invalid repair cumulative values, or legacy prompt-only values.
- **SC-008**: Token accounting adds no more than 5% to median end-to-end attempt duration on a representative small pilot, excluding the provider or tool execution time itself.
- **SC-009**: Existing complete agent-tool usage records remain usable without rerunning their tools, while no historical built-in prompt-only value is silently upgraded to exact or complete split usage.

## Assumptions

- Built-in-lane usage will be treated as estimated for this feature even when some providers could expose reported usage; adopting provider-reported built-in usage can be added later under a new accounting policy.
- The configured tokenizer package can separately tokenize the complete TestMap-supplied request and returned response. Model-specific framing or provider-injected hidden context remains outside the estimate and is disclosed by the estimated classification.
- Input and output are the required top-level token categories. Cached-input, cache-write, reasoning, thought, and tool-use subtypes remain source evidence and are folded into the two categories according to the accounting policy rather than becoming mandatory top-level comparison fields.
- The existing prompt estimate for agent-tool task cards remains diagnostic only and is not substituted for reported tool usage.
- Existing raw prompts, responses, and agent-tool artifacts continue to be retained under current storage and redaction practices.
- This feature changes cost measurement and analysis only; it does not alter candidate selection, generation budgets, repair limits, validation outcomes, or coverage and mutation measurement.
