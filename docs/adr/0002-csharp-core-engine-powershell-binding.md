---
status: accepted
---

# C# Core Engine with a thin PowerShell binding, not a PowerShell-classes domain model

Since [Decide internal record/type/collection data-model representation](https://github.com/HeyItsGilbert/mdbase-ps/issues/7), the domain model (`MdbRecord`/`MdbType`/`MdbCollection`, the write pipeline, the query engine, and — as of [Decide durable-runtime SQLite schema & run/lease state-machine mechanics](https://github.com/HeyItsGilbert/mdbase-ps/issues/23) — the durable runtime) had been designed as PowerShell classes under `mdbase/Classes/`, with only third-party dependencies (JsonSchema.Net, Celly, Microsoft.Data.Sqlite, the RFC 8785 canonicalizer) as vendored C# libraries. That domain-model-in-PowerShell shape forced the engine and PowerShell's own idioms and constraints together in one layer, and made the domain logic unreachable from any consumer that isn't `mdbase`'s own cmdlets.

We decided: the domain model moves entirely into a portable C# library, **Core Engine** (`Mdbase.Core`) — no PowerShell dependency, usable from any .NET consumer. The `mdbase` PowerShell module becomes a thin **Binding**: cmdlets call into the Core Engine and return its objects directly onto the pipeline (presentation via `.format.ps1xml`/`.types.ps1xml`), with no parallel PowerShell class layer re-wrapping them. This mirrors the spec's own architecture — Ch.14's "Minimum Durable Conformance" section frames durable conformance as belonging to *hosts*, naming "the Rust runtime and Connect binding" as separate things; mdbase-ps now follows the same engine/binding split rather than inventing a PowerShell-specific shape.

Both projects live in this repo for now (`src/Mdbase.Core/` + the existing `mdbase/` module layout), not split into separate repos — the Core Engine's API hasn't proven itself against a real consumer yet, so premature cross-repo versioning/release coordination isn't worth paying for. Extraction to its own repo once the API stabilizes is expected and cheap (a project boundary already exists; it's a `git filter-repo` and a new remote, not a redesign).

## Considered Options

- **Selective extraction** — pull only the SQLite runtime engine and CEL/JSON-Schema/CloudEvents glue into a small internal C# library, keep `MdbRecord`/`MdbType`/`MdbCollection` as PowerShell classes. Rejected: doesn't serve the goal of a reusable C# engine other .NET consumers can use directly — the most domain-meaningful types would still be PowerShell-only.
- **Two repos from the start** (`mdbase-core` + `mdbase-ps`). Rejected for now — adds release-coordination overhead before the Core Engine's public API shape has been proven against real usage; revisit once it has.

## Consequences

- [Survey PowerShell YAML frontmatter parsing options and dependency policy](https://github.com/HeyItsGilbert/mdbase-ps/issues/5) is **superseded**, not amended: Yayaml is a PowerShell module and can't be a dependency of a portable, PowerShell-independent Core Engine. The Core Engine takes a pure .NET YAML library (e.g. YamlDotNet) instead; a fresh ticket needs to make that survey/pick formally.
- `mdbase/Classes/` goes away entirely. Every domain type (`MdbRecord`, `MdbType`, `MdbCollection`, `MdbActivation`, `MdbCompiledWorkflow`, `MdbBridge`, the SQLite runtime layer from #23, etc.) is a C# type in `Mdbase.Core`; `MdbRecord`'s "immutable snapshot" framing (#7) maps directly onto a C# `record` with `init`-only properties.
- [Decide distribution & versioning approach](https://github.com/HeyItsGilbert/mdbase-ps/issues/15)'s vendoring mechanics need revisiting: the ALC-loader pattern built for vendoring *third-party* NuGet packages now also has to load a *first-party*, source-built `Mdbase.Core.dll` (with JsonSchema.Net/Celly/Sqlite/YamlDotNet/canonicalizer as its transitive dependencies) — likely simplifying to "load one entry assembly, let normal .NET dependency resolution pull the rest from a lib folder," but that needs to be decided explicitly, not assumed.
- [Decide testing strategy for the module](https://github.com/HeyItsGilbert/mdbase-ps/issues/14)'s three Pester trees need a peer: an xUnit/NUnit test project for `Mdbase.Core`'s own logic (state machines, CAS SQL, CEL binding, schema validation), with Pester's `tests/Unit/` narrowing to PowerShell-binding-layer behavior (cmdlet parameter binding, pipeline shape, `-WhatIf`) rather than domain logic.
- This is a foundational pivot against nearly every ticket resolved on [Map: mdbase-ps implementation plan](https://github.com/HeyItsGilbert/mdbase-ps/issues/1) from #7 onward. The *decisions* those tickets made (immutable snapshots, patch-based writes, the SQLite schema shape, the CEL activation-factory design, etc.) still hold — only the implementation language premise changes. Reconciling the map with this ADR (reopening #5, revisiting #15's distribution mechanics, extending #14's testing strategy) is follow-up wayfinder work, not done here.

Decided via grilling (`/grill-with-docs`), 2026-08-02.
