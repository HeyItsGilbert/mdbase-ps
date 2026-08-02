# YAML frontmatter parsing options for mdbase-ps

Research for [Survey PowerShell YAML frontmatter parsing options and dependency policy](https://github.com/HeyItsGilbert/mdbase-ps/issues/5), part of the [mdbase-ps implementation plan](https://github.com/HeyItsGilbert/mdbase-ps/issues/1).

Target runtime (decided in [#2](https://github.com/HeyItsGilbert/mdbase-ps/issues/2)): PowerShell 7.4+, .NET 8, cross-platform.

## What mdbase requires (spec Ch. 03 — Records And Frontmatter / YAML Profile)

- Parse UTF-8 Markdown files; frontmatter is the YAML mapping between the opening/closing `---` delimiters.
- **"SHOULD use a safe YAML parser and MUST NOT execute custom tags."** This rules out any parser/schema mode that resolves arbitrary `!!` tags to CLR types by default (the classic YAML deserialization footgun).
- **Scalar normalization to the JSON data model** before JSON Schema validation: mapping→object, sequence→array, string→string, integer/float→number, boolean→boolean, null→null.
- **Non-JSON YAML values — `NaN`, `Infinity`, binary values, and parser-specific timestamp objects — MUST be explicitly handled by the mdbase YAML profile before schema validation, or rejected with a clear diagnostic.** This is a real constraint: it's not enough to parse successfully, the parser's *scalar resolution* (which YAML schema/tag-set it uses to decide a bare scalar is a float, a timestamp, etc.) has to be controllable, because the default "smart" schema most YAML parsers ship with will silently turn `2024-01-01` into a native `DateTime`/timestamp object or `.nan`/`.inf` into a native float — exactly what mdbase says must instead be intercepted and either normalized or diagnosed.
- **Serialization** (write path, relevant once Core Write is in v1 scope): preserve unrelated body text and line-ending style, omit missing values (bare null = explicit null), quote empty strings, preserve array/object structure, deterministic key ordering when regenerating a file.

The real constraint, same shape as the JSON Schema and CEL surveys: not "can it parse YAML" (everything below can), but "can the module control *schema/tag resolution* precisely enough to implement mdbase's specific scalar-handling and custom-tag policy."

## Candidates surveyed

All PowerShell-facing YAML options in the ecosystem are wrappers around **YamlDotNet** (`aaubry/YamlDotNet`, MIT) — there is no independent PowerShell-native YAML parser. YamlDotNet itself (18.1.0, MIT, published 2026-06-26, multi-targets modern TFMs including net8.0) exposes exactly the control mdbase needs: `IYamlTypeConverter`/schema classes (`FailsafeSchema`, `JsonSchema`, `CoreSchema`, `DefaultSchema`) that govern which tags a bare scalar resolves to, and low-level parsing/emitting APIs for round-trip control. The question is how to consume it.

### Yayaml (`jborean93/PowerShell-Yayaml`) — recommended

- PowerShell Gallery module, MIT, by a well-known, actively-maintained-ecosystem author (also authors PSOpenAD, DSInternals, and other widely-used PS modules).
- **Requirements: PowerShell 5.1, or 7.4+** — the 7.4+ path matches the runtime decided in #2 exactly.
- Directly exposes schema control matching mdbase's needs: **YAML 1.2 (default), YAML 1.2 JSON Schema, YAML 1.1, and Failsafe schemas** are all selectable, plus a `New-YamlSchema`/`CustomSchema.cs` mechanism for defining a custom schema. The 1.2 JSON Schema option is the closest built-in match to "normalize scalars into the JSON data model" — it's the schema YAML itself defines as JSON-compatible (no `.nan`/`.inf`, no implicit timestamp resolution), which directly satisfies the bulk of the NaN/Infinity/timestamp handling requirement without mdbase-ps needing to hand-write scalar resolution rules.
- Finer control over scalar/map/sequence emission styles, and comment emission on `ConvertTo-Yaml` (parsing comments is explicitly not supported by any candidate here, including this one — not required by the spec).
- **Loads `YamlDotNet` in an Assembly Load Context (ALC), PowerShell 7+ only** — this is the standout differentiator. It exists specifically "to avoid DLL hell and cross assembly conflicts." That matters concretely for mdbase-ps: the module already plans to bundle JsonSchema.Net (#3) and Celly (#4) as assemblies; a third bundled assembly (raw YamlDotNet) raises real risk of colliding with a *different* YamlDotNet version already loaded in the user's session by `powershell-yaml` (below) — which is close to a de-facto standard in DevOps/CI tooling and very likely to already be loaded alongside mdbase-ps in real sessions. Yayaml's ALC isolation sidesteps that collision class entirely, for both its own YamlDotNet copy and (by the same mechanism, if mdbase-ps follows its pattern) any others.
- Actively developed: PS 7.4+ minimum floor, CI on GitHub Actions, doesn't carry powershell-yaml's PS-4/5-era design baggage.

### powershell-yaml (`cloudbase/powershell-yaml`) — considered, not recommended

- The long-standing, most widely-adopted PowerShell YAML module (Cloudbase Solutions, MIT-style license, copyright 2016–2026). Thin wrapper over YamlDotNet.
- Originally designed for PowerShell 4/5.1 (Windows PowerShell era); works on PS 7 and Linux but wasn't architected around PS 7's module-isolation story — **no Assembly Load Context isolation**, so it loads YamlDotNet directly into the default context, the classic source of "different modules want different YamlDotNet versions" conflicts.
- Actively maintained (0.4.12 released Jan 2025, open issues from mid-2025 still being triaged) but issue backlog suggests slower turnaround than Yayaml.
- Doesn't expose the same explicit schema-selection surface (JSON/Failsafe/1.1/1.2) as Yayaml — schema/tag behavior is less directly controllable from the cmdlet surface, which matters for mdbase's specific NaN/Infinity/custom-tag requirements.
- Verdict: the ecosystem-standard choice, but a strictly weaker technical fit than Yayaml for mdbase-ps's specific requirements (schema control, DLL-conflict avoidance), and mdbase-ps risks colliding with *this exact module* if a consuming session already has it loaded — another point in favor of Yayaml's ALC isolation, which avoids interfering with (or being interfered with by) `powershell-yaml` if both happen to be loaded together.

### Raw YamlDotNet, bundled directly (same pattern as #3/#4) — considered, not recommended as primary

- Consistent with the direct-dependency pattern chosen for JsonSchema.Net (#3) and Celly (#4): full control over `IYamlTypeConverter`s, custom schema construction, and emission — nothing here is technically out of reach.
- But unlike JSON Schema validation and CEL evaluation, where no mature idiomatic PowerShell-native option existed, YAML parsing in the PowerShell ecosystem already has that mature, purpose-built option (Yayaml) — including the exact DLL-isolation mechanism mdbase-ps would otherwise need to build itself to safely coexist with `powershell-yaml` and other YamlDotNet-bundling modules in the same session.
- Verdict: reinventing ALC-loading to bundle YamlDotNet raw would just re-derive Yayaml's own architecture with more code to maintain, for no net gain in control — mdbase-ps still needs a custom schema/converter layer either way, and Yayaml already hosts that layer with the isolation problem solved.

## Recommendation

Take **Yayaml** as a module dependency (`RequiredModules` on the PS 7.4+ path) rather than bundling raw YamlDotNet: use its **1.2 JSON Schema** (or a custom schema built via its `New-YamlSchema`/`CustomSchema` mechanism if the built-in JSON schema doesn't cover every mdbase edge case once implementation starts) to get scalar resolution that matches the JSON data model directly, intercept/diagnose the remaining NaN/Infinity/binary/timestamp edge cases the spec calls out explicitly, and rely on its Assembly Load Context isolation to avoid version conflicts with `powershell-yaml` or any other YamlDotNet-bundling module likely to be co-loaded in a real PowerShell session. This is the one survey of the three (JSON Schema, CEL, YAML) where "take a mature PowerShell-native module" beats "bundle the raw .NET library directly," specifically because that PowerShell-native option already solves a packaging problem (DLL isolation) the raw-library path would otherwise have to solve from scratch.

## Open follow-ups for later tickets (not blocking this survey)

- Whether Yayaml's built-in 1.2 JSON Schema fully covers mdbase's NaN/Infinity/binary/timestamp normalization-vs-rejection requirements, or whether a custom schema is needed, is an implementation-time detail for the collection-load/frontmatter-parsing design ticket (still fog on the map), once the internal data model (#7, still blocked) is settled.
- Serialization requirements (preserve body/line-endings, deterministic key ordering on write) are Core Write/Lifecycle concerns — deferred until the v1 conformance-profile scope ticket (#6) determines whether write support ships in v1.
- If Yayaml's `RequiredModules` dependency proves undesirable at the distribution-ticket stage (same "vendor vs. install dependency" question raised for JsonSchema.Net and Celly), the fallback is vendoring YamlDotNet with mdbase-ps's own ALC loader, mirroring Yayaml's architecture — not a dead end, just extra work deferred unless needed.
