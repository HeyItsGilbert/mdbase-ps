# Vendored v0.3 conformance fixtures

Source: https://github.com/callumalpass/mdbase-spec
Commit: `02388190b9287954139d7feac49d0e3e10c44cfe` (2026-08-03)
Path: `tests/v0.3/core/core-collection.yaml`

Vendored as a dated snapshot per #14's testing-strategy resolution ("Fixture YAML is
vendored as a dated snapshot... rather than fetched live from GitHub during test runs").
Refresh deliberately, not automatically, by re-copying the file from a newer upstream commit.

## Why only `core_collection`, not `schema_artifacts`

Issue #37's Testing Decisions name both `schema_artifacts` and `core_collection`. Inspecting
the real upstream suite (this commit) shows `schema_artifacts`'s tests use operations
(`json_schema_meta_validate`, `markdown_frontmatter_schema_validate`,
`yaml_document_schema_validate`, `embedded_json_schema_validate`, `type_pack_resources_validate`,
`json_document_schema_validate`) that do not appear in the upstream suite's own documented
"Adapter Operations" list (`tests/v0.3/README.md`) at all. They validate the upstream spec
package's *own* bundled schemas/examples/testbed fixtures against JSON Schema's meta-schema —
exactly the "local artifact checks that do not require a full v0.3 implementation" the same
README attributes to the upstream repo's own `scripts/check_v03_tests.py`, not to a per-language
adapter. Running it here would mean vendoring the entire upstream `schemas/v0.3/`, `examples/v0.3/`,
`standard-packs/`, and `testbed/v0.1/` trees to test the *upstream package's* internal
consistency — not `Mdbase.Core`'s behavior. `core_collection`'s tests use `validate`/`read`/
`get_types`/`get_type`/`create`, which *are* real adapter operations and map directly onto
`MdbCollection`'s surface.

## Excluded individual cases

`core_collection` itself reaches slightly past this spec's scope (issue #37 explicitly defers
Core Write and cross-file Uniqueness to their own future specs). `ConformanceFixtureRunner`
skips exactly these cases, each for a named reason:

- the whole "path policy and create behavior" group (`operation: create` — Core Write, #11/#12)
- `collection.uniqueness` (`collection.unique` — Collection Semantics' own future decomposition)

"collection links resolve valid ID-based link" and "collection links enforce validate_exists"
(Links, #9/#38) now run for real. The vendored fixture's `link_not_found` code and combined
`valid` flag are matched by folding `MdbRecord.LinkDiagnostics` alongside `ValidationDiagnostics`
in the runner's assertion helper — `MdbRecord.IsValid` itself stays schema-only by design (#38
keeps link diagnostics traceable to their own pipeline stage).

Every other case in the file runs for real against `Mdbase.Core`.

## Data contracts

Source: `tests/v0.3/data-contracts/data-contracts.yaml` at commit
`02388190b9287954139d7feac49d0e3e10c44cfe` (2026-08-03). The referenced
TaskNotes and data-contract fixture inputs are copied under `data-contracts/sources/`
so the suite remains an offline, dated snapshot. `DataContractsConformanceTests`
drives each case through `MdbCollection`'s public contract seams.


## CEL profile

Source: `tests/v0.3/cel/cel-profile.yaml` at commit
`02388190b9287954139d7feac49d0e3e10c44cfe` (2026-08-03). The full fixture
is copied under `cel/` so the suite remains an offline, dated snapshot.
`CelConformanceTests` runs its `query` and `get_types` cases through
`MdbCompiledQuery` and `MdbCollection`'s public type-membership seams.

The remaining `evaluate_cel` cases intentionally do not run in this Core
fixture adapter: Mdbase.Core does not expose a standalone CEL-evaluation API,
and the fixture's workflow cases and `file.hasTag` case are outside #40's
scope. Their record-query behavior is covered by the executable query and
membership cases; the matching host bindings are also covered by the
hand-written public-seam tests.