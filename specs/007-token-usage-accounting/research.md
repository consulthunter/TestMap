# Research: Token Usage Accounting

## Decision 1: Keep built-in usage estimated under one policy

**Decision**: Use the existing SharpToken `cl100k_base` encoder for both request and response content
in the built-in lane and classify every complete observation as `complete-estimated` under
`token-accounting-v1`.

**Rationale**: The user explicitly selected an estimated built-in lane. It produces one consistent
measurement across all configured providers and avoids provider/version-specific metadata handling.
The status and source prevent estimates from being mistaken for billed usage.

**Alternatives considered**: Extract provider-reported Semantic Kernel metadata; combine reported
usage when available with estimates otherwise; add OpenTelemetry capture. These would expand the
provider contract and create mixed precision within the built-in lane, contrary to the selected
minimal scope.

## Decision 2: Count TestMap-supplied message content, not provider framing

**Decision**: Chat input is the sum of tokenized TestMap system-message content and user-message
content. Inference input is the prompt content. Output is the tokenized response text. Unknown chat
framing, provider-injected context, and hidden reasoning remain outside the estimate.

**Rationale**: These are the exact textual inputs and outputs TestMap can observe consistently. The
estimate remains reproducible even when provider implementations differ.

**Alternatives considered**: Approximate provider-specific framing; concatenate all messages into one
synthetic string; claim billed usage. Framing rules are model-specific, concatenation introduces an
arbitrary boundary, and billed-usage claims would be unsupported.

## Decision 3: Extend the provider contract with only estimation input segments

**Decision**: Keep `GenerateAsync` returning text. Add the smallest provider-facing capability needed
to expose the content segments used for input estimation, with shared behavior in the existing
Semantic Kernel base.

**Rationale**: The base owns the chat system prompt and active generation mode. Exposing segments
avoids duplicating the prompt in the pipeline and avoids a mutable "last usage" property.

**Alternatives considered**: Return a new response object with usage; duplicate the system prompt in
the pipeline; add a token-accounting service. All add unnecessary surface or risk the counted request
diverging from the sent request.

## Decision 4: Reuse existing token columns and derive totals

**Decision**: Populate existing generation-step input/output columns; make existing generation step
and attempt totals nullable and canonical for new-policy rows; add attempt input/output and usage
metadata. Do not add an independently stored tool total.

**Rationale**: Most of the schema already exists. Deriving total from input plus output prevents
contradictions. A derived tool property provides uniform domain/export access without another column.

**Alternatives considered**: Add a new token-event table; add duplicate `total_tokens` columns; retain
non-null zero sentinels. These add joins/redundancy or violate missingness rules.

## Decision 5: Use explicit status, source, and policy

**Decision**: Use `complete-reported`, `complete-estimated`, `partial`, `missing`, and
`not-applicable`; retain a human-readable source; persist/export `token-accounting-v1`.

**Rationale**: Component nullability alone cannot distinguish missing from not applicable or reported
from estimated. Policy identity is required because tool cache/reasoning normalization can change.

**Alternatives considered**: Keep only `usage_available`; encode everything in `usage_source`; infer
status downstream. The existing boolean already disagrees between persistence and export and cannot
express the required states.

## Decision 6: Aggregate retries conservatively inside a step

**Decision**: Count input once for every attempted provider invocation. Count output for each returned
response, including zero for an observed empty response. If an invocation throws without observable
output, preserve known components but classify the logical step as partial and leave total null.

**Rationale**: Retries consume cost and cannot be omitted. Missing output must not be assumed zero.

**Alternatives considered**: Count only the successful retry; count an exception as zero output;
create one persisted row per invocation. The first two undercount; the last creates a new grain and
table beyond the requested scope.

## Decision 7: Derive repair cumulative values in orchestration

**Decision**: Persist each attempt's own components and derive cumulative input/output/total in the
existing ordered repair loop and canonical export. Do not persist separate repair usage rows.

**Rationale**: Parent links and attempt order already reconstruct the chain. This matches the current
transient cumulative-total pattern and avoids duplicated state.

**Alternatives considered**: Store cumulative values on every attempt; create a repair-cost table;
derive only in Python. Persisting duplicates can become stale, while Python-only derivation leaves
the canonical export incomplete.

## Decision 8: Harden existing tool parsers in place

**Decision**: Keep current artifact preference and parser methods, add explicit aggregate/detail
precedence, cover Gemini JSON mode, and scope per-call summation where the artifact is incremental.

**Rationale**: All seven tools already have fixtures and parsers. Focused corrections are lower risk
than replacing them with a generic telemetry subsystem.

**Alternatives considered**: Normalize raw artifacts into a new intermediate event store; require all
tools to emit one format. Both are disproportionate and would require runner/image changes.

## Decision 9: Keep results schema 4.0 and version token policy

**Decision**: Add optional cumulative-component and policy columns to schema 4.0 and use the explicit
token policy to distinguish the new behavior.

**Rationale**: Existing token columns retain their definitions; previously unavailable built-in
components become measured estimates with explicit status. The added columns do not change row grain
or existing non-token fields. A full result-schema bump would force unrelated assertion/coverage
fixtures and readers to migrate for an additive cost feature.

**Alternatives considered**: Bump to schema 5.0; create a token sidecar. A policy version is sufficient
for the semantic change, and a sidecar would duplicate attempt identity and complicate joins.

## Decision 10: Preserve historical rows without false backfill

**Decision**: Existing built-in prompt-only totals remain stored but receive legacy/missing metadata
and are excluded from complete split-token analysis. Existing tool rows with both stored components
may derive total and complete-reported status; partial/missing rows remain so.

**Rationale**: Exact historical built-in output and retry usage was never retained. Tool artifacts and
components already provide defensible evidence.

**Alternatives considered**: Treat old total as input; estimate output from stored responses during
migration; clear old totals. The first two would silently change meaning, while clearing destroys
useful legacy evidence.
