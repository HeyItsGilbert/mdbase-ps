# CEL expression evaluation implementations for mdbase-ps

Research for [Survey CEL expression evaluation implementations for the target runtime](https://github.com/HeyItsGilbert/mdbase-ps/issues/4), part of the [mdbase-ps implementation plan](https://github.com/HeyItsGilbert/mdbase-ps/issues/1).

Target runtime (decided in [#2](https://github.com/HeyItsGilbert/mdbase-ps/issues/2)): PowerShell 7.4+, .NET 8, cross-platform.

## What mdbase requires (spec Ch. 10 — CEL Profile)

- CEL appears in `match.expr`, query filters/projections/summaries, lifecycle guards, and workflow variables/conditions/inputs/iteration/run-policy. (`match.where` uses a separate structured predicate language, not CEL.)
- Every expression location has a fixed **context contract** — a specific set of system bindings (`record`, `raw`, `present`, `file`, `note`, `projection`, `this`, `old`, `operation`, `event`, `workflow`, `trigger`, `steps`, `vars`, `item`) that must be supplied per-location, with missing/null semantics distinct from CEL's own presence rules (`present.raw.<field>` vs `has()`).
- Required host functions: `now()`, `today()`, `duration(string)`, plus profile-added helpers (`file.inFolder(path)`, and with the Links profile: `link(value)`, `file.hasTag(tag)`, `file.hasLink(linkValue)`, `file.asLink()`, `linkValue.asFile()`).
- **All stored expressions must compile (parse + type-check) during preflight** — parse/type errors invalidate the containing type/query/lifecycle/workflow object before it's ever evaluated against a record.
- **Mandatory limits**: expression source ≥64 KiB, AST depth ≥100, link traversal depth ≥10 as portable minimums; hosts should also bound list iteration, elapsed time, and memory, and report exceeded limits as diagnostics.
- Data model is dynamic per-collection: a record's effective/raw frontmatter is an arbitrary map shape discovered at runtime from Markdown/YAML, not a fixed compiled type — so a CEL engine wired to a static protobuf message schema is a poor fit; it needs to bind against plain maps/dictionaries per evaluation.

## Candidates surveyed

### Celly (`bsidio/celly`) — recommended

- NuGet: `Celly` 1.2.0, Apache-2.0, published 2026-07-18. **Zero dependencies** for the core package; `Celly.Protobuf`/`Celly.Protovalidate` are separate opt-in packages, not needed here.
- Targets **.NET 8.0** — matches the runtime decided in #2 directly (badge confirms `.NET 8.0`).
- **Pure managed C#** — no WASM, no native bindings, no Go-compiled artifacts. Simplifies cross-platform packaging (Windows/Linux/macOS, per #2) since there's no per-platform native binary to ship.
- **100% conformance**: 2,456/2,456 of the official cel-spec conformance suite, verified in CI on Linux/Windows/macOS, cross-checked with a differential fuzzer against cel-go (zero divergences reported). This is the strongest correctness evidence of any candidate surveyed.
- **Binds directly to .NET dictionaries/lists/primitives** via `MapActivation` (`program.Eval(dictionary)`) — no protobuf message definitions required. This is the key fit: mdbase records are dynamically-shaped maps discovered per-collection, not a fixed compiled schema, and Celly's activation model is built for exactly that (dictionary-in, `CelValue`-out), with an `IActivation` interface available for lazy per-name resolution — a natural fit for mdbase's per-context reserved bindings (`record`, `raw`, `present`, `file`, ...), since `TryFind` is only called for names an expression actually references.
- **Custom function registration** (`CelEnvSettings.ConfigureFunctions` + `FunctionDeclarations`) covers all of mdbase's required host functions (`now()`, `today()`, `duration(string)`, `file.inFolder()`, and the Links-profile helpers) with proper checker-level type signatures — satisfying the "compile during preflight" requirement, since `FunctionDecl`/`OverloadDecl` let the type checker validate calls before any record is evaluated.
- **Built-in evaluation budget primitives** (`EvalLimits` — iteration cap + `CancellationToken` — and static pre-evaluation `CelEnv.EstimateCost`) map almost directly onto mdbase's Ch. 10 Limits requirement (bound AST depth, evaluation work, list iteration, elapsed time) without mdbase-ps needing to hand-roll a cost estimator.
- Immutable, thread-safe `CelEnv`/`CelProgram` — compile once per type/query/workflow, evaluate concurrently across every record in a collection.
- Fastest .NET CEL implementation benchmarked (~1.2× faster than Cel.NET, ~6-9× faster than TELUS `Cel`), and faster than the reference Go implementation (cel-go) on comprehension-heavy expressions — relevant since mdbase queries filter/project across every record in a collection.
- Risk to note: **young project** (1.0 tagged 2026-07-18, ~2 weeks old at time of this survey) from a single primary author. Mitigated by: rigorous conformance-suite + fuzz verification methodology, permissive Apache-2.0 license (forkable if abandoned), and zero transitive dependencies (small surface to vendor/pin if needed).

### Cel.NET (`rayokota/cel.net`) — rejected

- NuGet: `Cel.NET` 1.1.0, Apache-2.0. Multi-targets `net8.0` (also `net10.0`, `.NETFramework4.6.2`, `.NETStandard2.1`).
- Heavy dependency graph: `Antlr4.Runtime.Standard`, `Antlr4BuildTasks`, `Apache.Avro`, `Google.Protobuf`, `Grpc.Net.Client`, `Newtonsoft.Json`, `NodaTime`. Pulls in gRPC/Avro/protobuf machinery mdbase-ps has no other use for, and reintroduces Newtonsoft.Json alongside the System.Text.Json-based stack already chosen for JSON Schema validation (#3) — splits the JSON/serialization surface for no benefit.
- Measured slower than Celly (~1.2× per Celly's published benchmarks).
- Verdict: viable in principle (does hit .NET 8, is a real CEL port) but the dependency weight and JSON-stack mismatch make it a worse fit than a zero-dependency alternative that also outperforms it.

### Cel (TELUS Labs, `telus-labs/cel-net`) — rejected

- NuGet: `Cel` 0.3.2, Apache-2.0. ANTLR-grammar-based (`.g4` file, generated lexer/parser).
- **Protobuf-message-centric type model**: custom types must be defined as protobuf messages with descriptors registered at CEL environment startup (`CelEnvironment(fileDescriptors, defaultNamespace)`). mdbase record shapes are discovered dynamically per-collection from Markdown/YAML type files at runtime — there's no compile-time `.proto` to generate from, so every collection load would need to synthesize protobuf descriptors on the fly just to bind field access. Awkward, high-friction fit.
- Low adoption signal (26 stars, 5 forks) relative to alternatives.
- Measured ~6-9× slower than Celly per Celly's published benchmarks (plausible given ANTLR-generated parsing vs. Celly's hand-written recursive-descent).
- Verdict: rejected primarily on the protobuf-type-model mismatch, reinforced by weaker performance and adoption.

## Recommendation

Take a direct dependency on **Celly** (`bsidio/celly`, Apache-2.0, zero dependencies, targets net8.0): bind mdbase's per-context reserved names (`record`, `raw`, `present`, `file`, `note`, etc., per Ch. 10's evaluation-context table) through a custom `IActivation` implementation for lazy, context-correct resolution; register `now()`, `today()`, `duration(string)`, and profile-added link/file helpers via `ConfigureFunctions`/`FunctionDeclarations`; compile every stored expression through `env.Compile()` at type/query/lifecycle/workflow preflight time (satisfying the "must compile during preflight" requirement structurally, since `Compile` throws on parse errors and `Check` surfaces type errors before any `CelProgram` exists); and configure `EvalLimits` to enforce mdbase's minimum-supported limits (64 KiB source, AST depth 100, plus mdbase-ps's own list-iteration/elapsed-time bounds).

This does not need a build-vs-buy call beyond "which of the three existing .NET CEL implementations" — authoring a CEL engine from scratch is out of scope for this module; Celly's conformance rigor (100% of the official suite, fuzzed against cel-go) makes "buy" the clear answer, and among the three real options it's the only one whose data-binding model (dynamic maps, not protobuf) matches mdbase's dynamically-typed record model.

## Open follow-ups for later tickets (not blocking this survey)

- Exact mapping of each Ch. 10 evaluation context (inferred match / query / query summary / lifecycle guard / workflow variants) to a concrete `IActivation` implementation belongs in the query-engine and lifecycle design tickets (still fog on the map), once the internal data model (blocked ticket) is settled.
- `file.hasLink`/`asFile`/link traversal depth-10 enforcement is Links-profile-specific and depends on the v1 conformance-profile scope decision (open ticket) determining whether Links ships in v1.
- Whether to vendor the Celly DLL vs. take a NuGet-at-install dependency is a distribution-ticket concern (`Not yet specified` on the map), same as the JSON Schema validator distribution question raised in #3's research.
