# JSON Schema 2020-12 validators for mdbase-ps

Research for [Survey JSON Schema 2020-12 validators for the target runtime](https://github.com/HeyItsGilbert/mdbase-ps/issues/3), part of the [mdbase-ps implementation plan](https://github.com/HeyItsGilbert/mdbase-ps/issues/1).

Target runtime (decided in [#2](https://github.com/HeyItsGilbert/mdbase-ps/issues/2)): PowerShell 7.4+, .NET 8, cross-platform.

## What mdbase requires (spec Ch. 06 — JSON Schema Profile)

- Base dialect: JSON Schema **2020-12**. Type files declare `schema.dialect: json-schema-2020-12`.
- Required keyword baseline: `type`, `required`, `properties`, `additionalProperties`, `items`, `enum`, `const`, `oneOf`, `anyOf`, `allOf`, `if`/`then`/`else`, `minimum`/`maximum`, `exclusiveMinimum`/`exclusiveMaximum`, `multipleOf`, `minLength`/`maxLength`, `pattern`, `minItems`/`maxItems`/`uniqueItems`, `$defs`, local `$ref`, annotations (`title`, `description`, `default`, `examples`).
- `format` is annotation-only in the base dialect, **except** mdbase requires assertion behavior for `date`, `date-time`, `time` (RFC 3339; `date-time` must carry `Z` or a numeric offset) — this needs an assertion vocabulary/custom format validators layered on top of a validator that otherwise treats `format` as non-asserting.
- `$ref` handling has mdbase-specific policy, not just spec-compliant resolution:
  - fragment-only `$ref` within an embedded schema — required.
  - a type wrapper's `schema.ref` to a local JSON file, base URI = the type file's directory, resolved path **must stay inside the collection root or installed pack root** (symlinks resolved before the boundary check) — escapes produce `schema_ref_forbidden`.
  - nested file-to-file `$ref` is the optional `external_schema_refs` feature — same boundary rule, plus cycle detection (`schema_ref_cycle`) and unresolved-ref reporting (`schema_ref_unresolved`).
  - **no network fetches ever** during validation; canonical `https://mdbase.dev/schemas/...` ids resolve only from a bundled registry, other HTTP(S) refs are `schema_ref_forbidden` unless a non-portable extension enables them.
- Diagnostics must map failing keywords to `schema_<keyword>` codes (snake_case, e.g. `schema_additional_properties`, `schema_unevaluated_properties`) and should carry the JSON Pointer/field path, schema location, type name, severity, and message — this needs a validator that exposes **structured, per-keyword evaluation results**, not just pass/fail.

These custom-resolver and structured-diagnostic requirements are the real constraint — not raw 2020-12 support, which several libraries have.

## Candidates surveyed

### JsonSchema.Net (`json-everything`, by Greg Dennis) — recommended

- NuGet: `JsonSchema.Net`, MIT licensed, actively maintained, 44.7M+ downloads.
- Targets **.NET 8.0** and .NET Standard 2.0 — matches the PS 7.4+/.NET 8 target decided in #2.
- Full JSON Schema **2020-12** vocabulary support (draft 6 and later), including `$defs`, `$ref`/`$dynamicRef`, `if`/`then`/`else`, `unevaluatedProperties`, and every keyword in mdbase's required baseline.
- Built on `System.Text.Json` — no Newtonsoft dependency, keeps the module's JSON stack uniform with PowerShell's own `ConvertFrom-Json`/`ConvertTo-Json`.
- Exposes `EvaluationOptions` with a pluggable `SchemaRegistry` and reference resolution — lets mdbase-ps register the bundled canonical `https://mdbase.dev/schemas/v0.3/...` schemas locally, register per-collection local file refs with a custom base URI, and refuse any resolution outside that registry (satisfies "no network fetch" and the collection-root boundary policy directly, rather than fighting a resolver that phones out by default).
- `EvaluationResults` (detailed/verbose output format) gives per-keyword pass/fail with instance location (JSON Pointer) and schema location — the shape mdbase's `schema_<keyword>` diagnostic mapping needs.
- `format` is annotation-only by default (per spec, correct base behavior); `EvaluationOptions.RequireFormatValidation = true` plus registering `date`/`date-time`/`time` format handlers gives the mdbase-required assertion behavior for exactly those three formats without over-asserting `email`/`uri`/etc.
- **This is the same library PowerShell 7.4+ ships internally** — `Test-Json` was rewritten in PS 7.4 to use `System.Text.Json` + `JsonSchema.Net` instead of `NJsonSchema`/Newtonsoft (see PowerShell/PowerShell#18141). Confirms it's the idiomatic, already-battle-tested-in-PowerShell-core choice for this runtime, and later versions than the one PowerShell core happens to pin can be referenced directly.

### `Test-Json` (built-in cmdlet) — insufficient alone

- Available in every PS 7.4+ session with zero added dependency, and internally is JsonSchema.Net.
- But it's a fixed high-level wrapper: no exposed hook to supply a custom `SchemaRegistry`/resolver, so it can't enforce mdbase's collection-root boundary, forbid network `$ref`s, or do fragment-vs-file-ref distinction. It also doesn't expose per-keyword structured diagnostics (`schema_<keyword>` codes, JSON Pointer paths) — it's pass/fail plus a flat error-message list.
- Verdict: not viable as the validation engine itself, but confirms the platform default library choice. mdbase-ps should take a **direct dependency on `JsonSchema.Net`** (bundled assembly via the module, same pattern `powershell-yaml` uses for `YamlDotNet`) rather than shelling through `Test-Json`.

### NJsonSchema — rejected

- Max support is JSON Schema **draft 7** (confirmed: this is exactly why PowerShell core replaced it with JsonSchema.Net in 7.4). Does not reach 2020-12. Disqualified outright.

### Newtonsoft `Json.NET Schema` — rejected

- Supports newer drafts but is **commercially licensed** for production/business use beyond a free tier. Wrong fit for an OSS module with no budget for per-seat/business licensing, and pulls in Newtonsoft.Json instead of System.Text.Json, splitting the JSON stack from what PowerShell 7.4+ uses natively.

### Corvus.JsonSchema — considered, not recommended for v1

- Code-generation-first model (generates C# types from schema at build time) rather than a runtime validator against arbitrary/dynamic schemas loaded from Markdown type files at runtime. mdbase type schemas are discovered and loaded dynamically per-collection, not known at module-compile-time, so Corvus's core value proposition doesn't fit this use case. Heavier dependency surface for no benefit here.

## Recommendation

Take a direct dependency on **JsonSchema.Net**, bundled as an assembly with the module (same distribution pattern the ecosystem already uses for `YamlDotNet` in `powershell-yaml`): pin a version targeting `net8.0`, wire a custom `SchemaRegistry` per loaded collection that (a) preloads the bundled canonical `https://mdbase.dev/schemas/v0.3/*` schemas, (b) resolves `schema.ref` local-file references relative to the type file's directory with the collection/pack-root boundary check (reject + `schema_ref_forbidden` on escape, including post-symlink-resolution), (c) refuses any other network/HTTP(S) reference resolution outright, and (d) enables `RequireFormatValidation` with custom `date`/`date-time`/`time` format handlers only. Use `EvaluationResults` (detailed output) to build the `schema_<keyword>` diagnostic codes with JSON Pointer paths the spec requires.

This does not need a build-vs-buy decision beyond "which validator library" — no case for hand-writing a validator; JsonSchema.Net's public API (registry, resolver hooks, structured results) covers every mdbase-specific policy layered on top of base 2020-12 conformance.

## Open follow-ups for later tickets (not blocking this survey)

- Exact `schema_ref_cycle` / `schema_ref_unresolved` detection wiring belongs in the collection-load design ticket, once the internal data model (blocked ticket) is settled.
- Whether/how to vendor the JsonSchema.Net DLL vs. take a `RequiredModules`/NuGet-at-install dependency is a distribution-ticket concern (`Not yet specified` on the map), not this survey.
