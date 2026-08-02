<!--
Vendored snapshot of the mdbase specification.
Source: https://mdbase.dev/spec/
Channel at fetch time: v0.3-current (rolling; not a pinned tag)
Fetched: 2026-08-02
Fetched via: read tool (reader-mode extraction of the live page)

This is a reproducibility snapshot for mdbase-ps wayfinder map decisions
that cite specific chapter sections. Refresh deliberately (not automatically)
if a ticket's spec citation is ever disputed against the live channel — see
wayfinder map issue #1 and https://github.com/HeyItsGilbert/mdbase-ps/issues/1.
MIT-licensed per the spec's own License section (00-overview).
-->

URL: https://mdbase.dev/spec/
Content-Type: text/html
Method: native

---

---
canonical: https://mdbase.dev/spec/
meta-description: The full mdbase specification for typed markdown collections.
meta-mdbase-spec-channel: v0.3-current
meta-og:description: The full mdbase specification for typed Markdown collections.
meta-og:title: mdbase specification v0.3
meta-og:type: article
meta-og:url: https://mdbase.dev/spec/
meta-theme-color: #fcfcfd
meta-viewport: width=device-width, initial-scale=1
title: Specification - mdbase
---

# [#](#00-overview)00. Overview

## [#](#abstract)Abstract

This specification defines the behavior of tools that treat folders of
Markdown files as typed, queryable, link-aware data collections. It covers
collection discovery, JSON Schema types, validation, links, CEL queries,
ordinary records that save named views, record operations, lifecycle policy,
and optional runtime workflows.

## [#](#motivation)Motivation

Markdown files with YAML frontmatter are a common way to store structured
content. The pattern appears in static site generators, knowledge management
tools such as Obsidian, documentation systems, agent workspaces, and AI agent
frameworks that use Markdown for persistent state.

These ecosystems need consistent conventions for frontmatter structure,
validation, querying, links, and updates. This specification defines one
coherent set of behaviors so that:

- a CLI tool and an editor plugin can operate on the same files with consistent
semantics
- an AI agent can read and write Markdown files that a human can inspect and
edit
- tool authors can implement a shared behavior contract
- collections can move between conforming tools

### [#](#intended-implementers)Intended implementers

**CLI tools** for querying, validating, and manipulating Markdown collections
from the command line.

**Editor plugins** for applications such as Obsidian and VS Code that provide
validation, completion, navigation, and query interfaces.

**Libraries** in different languages that applications can use to work with
typed Markdown.

**AI agent frameworks** that need structured, human-readable persistent
storage.

**Runtime hosts** that connect collection events to declared actions and
workflows.

## [#](#what-a-conforming-tool-does)What a conforming tool does

Depending on its conformance profiles and optional features, a tool implementing
this specification:

1. **Recognizes collections** by the presence of an `mdbase.yaml` configuration
file.
2. **Loads type definitions** from Markdown files that wrap JSON Schema.
3. **Matches records to types** through explicit declarations or configured
match rules.
4. **Validates frontmatter** against matched schemas and collection-aware
rules.
5. **Resolves links** between records and exposes link-aware metadata.
6. **Executes queries** using CEL expressions for filtering, ordering, and
projection.
7. **Executes saved view records** when it advertises the optional
`view_records` feature.
8. **Performs record operations** with validation, reference handling, and
lifecycle-managed values.
9. **Loads runtime contracts and workflows** when it supports active behavior.

Conformance profiles define the expected behavior for each capability and its
dependencies.

## [#](#design-principles)Design Principles

**Files are the source of truth.** Tools read from and write to the filesystem.
Indexes, caches, and derived databases can be rebuilt from collection state.

**Human-readable first.** Persistent collection data uses open text formats. A
user with a text editor can read and modify every record and type file.

**Progressive strictness.** A collection can begin with untyped records. Types,
validation, link rules, and lifecycle policy can be introduced incrementally.

**Portable.** Conforming tools share the same record and collection semantics.
Implementation-specific features use explicit extension namespaces.

**Git-friendly.** Durable collection state is stored as text that can be
reviewed in diffs and versioned with the rest of a project.

**Standard building blocks.** JSON Schema describes frontmatter shape. CEL
provides portable expressions. Named mdbase sections describe behavior that
depends on collection context.

## [#](#how-it-works)How It Works

### [#](#a-collection-is-a-folder-with-an-mdbaseyaml-marker)A collection is a folder with an `mdbase.yaml` marker

    my-project/
    ├── mdbase.yaml            # marks this folder as a collection
    ├── _types/                # type definitions
    │   ├── task.md
    │   └── person.md
    ├── tasks/
    │   ├── fix-bug.md         # a task record
    │   └── write-docs.md
    └── people/
        └── alice.md           # a person record

The minimal configuration declares the specification version:

    # mdbase.yaml
    spec_version: "0.3.0"

### [#](#types-are-defined-as-markdown-files)Types are defined as Markdown files

A type describes a category of records. Type files usually live in `_types/`.
Their frontmatter contains a JSON Schema and optional mdbase collection rules;
their body documents the type for people and tools.

    ---
    kind: mdbase.type
    name: task
    version: 1

    match:
      path_glob: "tasks/**/*.md"

    schema:
      dialect: json-schema-2020-12
      value:
        $schema: "https://json-schema.org/draft/2020-12/schema"
        type: object
        required: [type, title]
        additionalProperties: false
        properties:
          type:
            const: task
          title:
            type: string
            minLength: 1
          status:
            enum: [open, in_progress, done]
          priority:
            type: integer
            minimum: 1
            maximum: 5
          assignee:
            type: string
          tags:
            type: array
            items: { type: string }

    collection:
      read_defaults:
        status: open
      links:
        assignee:
          target_type: person
          validate_exists: true
    ---

    # Task

    A task represents a unit of work. Set `status` to track progress.

JSON Schema validates the persisted frontmatter object. The `collection` section supplies rules that require collection context, including effective
read defaults, links, uniqueness, and path policy.

### [#](#data-contracts-make-type-meaning-portable)Data contracts make type meaning portable

Types can implement exact, versioned data contracts stored under `_contracts/`. A contract uses JSON Schema for its normalized record interface
and optional binding. The type's `implements` entry maps contract fields to
record fields. Record contracts normally describe compact application
semantics rather than storage layouts or external wire formats.

Several types can implement one contract, one type can implement several
contracts, and several applications can consume the same contract view.
Discovery returns the complete implementation set; it never
silently picks a provider. Data contracts are passive interoperability and do
not grant access.

### [#](#records-are-markdown-files-with-typed-frontmatter)Records are Markdown files with typed frontmatter

A record provides field values in frontmatter and free-form Markdown in its
body. Records may declare a type explicitly or match a type through configured
rules.

    ---
    type: task
    title: Fix the login bug
    status: in_progress
    priority: 4
    assignee: "[[alice]]"
    tags: [bug, auth]
    ---

    The login form rejects addresses containing a `+` character.

### [#](#queries-filter-and-sort-records-with-cel)Queries filter and sort records with CEL

Queries are YAML objects with optional clauses for filtering, ordering,
projection, and pagination:

    types: [task]
    where: 'status != "done" && priority >= 3'
    order_by:
      - field: priority
        direction: desc
    limit: 20

CEL expressions can access frontmatter values, file metadata, dates, lists,
and resolved links:

    status == "open" && "urgent" in tags
    due_date < today()
    assignee.asFile().team == "engineering"

### [#](#view-records-save-reusable-queries)View records save reusable queries

A collection can define the ordinary `view` type and store one or more named
queries in a Markdown record. Shared query scope, named-view filters,
projections, ordering, grouping, and summaries remain machine-readable, while
the Markdown body documents the view for people. Optional presentation metadata
can select a renderer without changing query results.

### [#](#validation-is-progressive)Validation is progressive

Files in a collection can remain untyped records. Types can be added
incrementally, and validation severity is configurable as `off`, `warn`, or `error`. JSON Schema controls field shape and unknown-property handling.
Collection rules add checks that depend on other records or paths.

### [#](#links-connect-records-across-the-collection)Links connect records across the collection

Records can reference each other with wikilinks such as `[[alice]]`, Markdown
links such as `[Alice](../people/alice.md)`, or declared path values. Link-aware
tools resolve targets, extract links and tags from record bodies, traverse
links in queries, and can update references during rename operations.

### [#](#lifecycle-policy-manages-values-during-writes)Lifecycle policy manages values during writes

Lifecycle policy can assign IDs, timestamps, slugs, copied values, and literals
during create and update operations. Lifecycle runs before final validation, so
managed fields participate in the same schema and collection checks as supplied
frontmatter.

### [#](#runtime-contracts-describe-active-behavior)Runtime contracts describe active behavior

Optional runtime records describe providers, events, actions, capabilities,
policies, workflows, runs, and checkpoints. A runtime host loads these
contracts, validates their data, and connects declared workflows to available
event and action implementations.

### [#](#conformance-is-profile-based)Conformance is profile-based

Implementations claim the profiles they support, such as Core Read, Collection
Semantics, Links, Query, Core Write, Lifecycle, Runtime Contracts, Workflow,
and Watch. Profile dependencies keep those claims precise and independently
testable.

## [#](#specification-structure)Specification Structure

| Document | Description |
| --- | --- |
| [01-concepts.md](#section-01) | Core terminology and data model |
| [02-collection-layout.md](#section-02) | Collection discovery, paths, and reserved state |
| [03-records-and-frontmatter.md](#section-03) | Markdown parsing and YAML value semantics |
| [04-configuration.md](#section-04) | The `mdbase.yaml` configuration file |
| [05-type-files.md](#section-05) | JSON Schema type wrappers and metadata |
| [05-data-contracts.md](#section-05) | versioned data contracts, type implementations, and transactional type packs |
| [06-json-schema-profile.md](#section-06) | Supported JSON Schema vocabulary and reference rules |
| [07-collection-semantics.md](#section-07) | Matching, defaults, uniqueness, links, and paths |
| [08-links.md](#section-08) | Link syntax, resolution, traversal, and backlinks |
| [09-lifecycle.md](#section-09) | Managed values and mutation-time policy |
| [10-cel-profile.md](#section-10) | Portable expressions and host bindings |
| [11-querying.md](#section-11) | Filters, ordering, projection, and result envelopes |
| [12-operations.md](#section-12) | Read and write operations, concurrency, and diagnostics |
| [13-runtime-contracts.md](#section-13) | Providers, events, actions, capabilities, and policy |
| [14-workflows.md](#section-14) | Workflow records and execution semantics |
| [15-migrations-and-compatibility.md](#section-15) | Migration from earlier versions and compatibility |
| [16-conformance.md](#section-16) | Profiles, claims, fixtures, and runners |

The [portable interoperability testbed](/testbed/) runs neutral contract,
event/action, and durable-runtime scenarios through black-box adapters. Its
canonical transcripts make cross-language and cross-application behavior
directly comparable without making one product's internal API normative.

## [#](#versioning)Versioning

This specification uses semantic versioning. The current version is **0.3.0**.
Collections declare their specification version with `spec_version` in `mdbase.yaml`. Tools declare the profiles and versions they implement.

The optional runtime-contract and workflow vocabulary has an independent
profile version. The runtime profile defined by this specification is **0.2**.

## [#](#normative-language)Normative Language

The keywords `MUST`, `MUST NOT`, `SHOULD`, `SHOULD NOT`, and `MAY` are to be
interpreted as described in RFC 2119.

Draft notes use ordinary prose and are non-normative.

## [#](#license)License

This specification is released under the [MIT License](https://opensource.org/licenses/MIT).

# [#](#01-concepts)01. Concepts

## [#](#collection)Collection

A collection is a directory tree identified by an `mdbase.yaml` file. It
contains Markdown records, type files, optional data contract files, optional
runtime records, and optional derived state.

A collection root is the directory containing the active `mdbase.yaml`.

## [#](#record)Record

A record is a Markdown file in the collection that is not excluded, not a type
or data contract file, and not another reserved collection file. A record has:

- a collection-relative path
- optional YAML frontmatter
- a Markdown body
- derived file metadata such as basename, extension, folder, size, and modified
time

The persisted frontmatter object is the raw record value. Effective read values
may additionally include `collection.read_defaults`.

## [#](#type)Type

A type is a Markdown file, usually under `_types/`, whose frontmatter has `kind: mdbase.type`. The type frontmatter wraps a JSON Schema and optional
mdbase sections.

Types define shape and collection semantics for matching records. Runtime
providers and workflows supply executable behavior.

## [#](#data-contract)Data Contract

A data contract is a versioned `mdbase.contract` control file that defines a
portable record interface and optional implementation binding using JSON Schema
2020-12. A record contract normally names shared application semantics; event
and action contracts describe complete messages.

A type implements a data contract through its top-level `implements` section.
The contract does not replace the type, select a provider, assign ownership, or
grant access. Several types can implement one contract, and several
applications can consume the same implementing type. One type can contain
separate implementations of several contracts.

## [#](#schema)Schema

In v0.3, "schema" means JSON Schema 2020-12 unless explicitly qualified.

`schema.value` validates persisted frontmatter object shape. mdbase-specific
sections such as `match`, `collection`, `lifecycle`, and `runtime` are outside
the JSON Schema payload.

## [#](#match)Match

Matching decides which type or types apply to an existing record. A record can
match no types, one type, or multiple types.

Explicit type declarations take precedence over inferred match rules. When a
record matches multiple types, it is valid only if it validates against every
matched type's JSON Schema and every matched type's mdbase collection
validators.

## [#](#collection-semantics)Collection Semantics

Collection semantics are rules that require knowledge of the file tree or
runtime context. Examples:

- link parsing and target resolution
- cross-file uniqueness
- effective read defaults
- path generation
- display metadata
- type matching
- path safety

These rules extend record validation with collection context.

## [#](#lifecycle)Lifecycle

Lifecycle policy runs during mutating operations. It can materialize managed
values such as IDs, creation timestamps, modification timestamps, slugs, and
simple transforms.

Lifecycle policy is deterministic operation behavior within Core Write. It runs
from type policy during the active mutation.

## [#](#expression)Expression

Portable v0.3 expressions use the mdbase CEL profile. Expressions appear in
queries, projections, runtime conditions, workflow input templates, and optional
lifecycle guards.

`match.where` uses the standalone structured predicate language defined in
Chapter 07.

## [#](#view)View

A view is an ordinary Markdown record whose matched type is `view`. It stores
shared query scope and one or more named queries, with optional advisory
presentation metadata.

Views do not introduce a second query engine. A view-aware tool resolves a
named view to the query model from Chapter 11 and executes it through the Query
profile. Tools that do not support view execution continue to read and validate
view files as ordinary typed records.

View records are passive collection data. Rendering a view, registering a
renderer, or connecting user interaction to actions may be tool- or
runtime-specific, but the record itself is not a runtime contract.

## [#](#link)Link

A link is a frontmatter value or body reference that can resolve to another
record. mdbase recognizes wikilinks, Markdown links, and bare path strings where
a field is declared as link-aware.

JSON Schema validates the local string or array shape. `collection.links` declares link meaning, target type, and existence requirements.

## [#](#runtime)Runtime

A runtime is a process, plugin, daemon, CLI, CI job, or agent that executes
runtime-profile behavior for a collection.

The core collection model is runtime-neutral. Runtime records make active
behavior portable and inspectable without making every implementation a runtime.

## [#](#runtime-contract)Runtime Contract

A runtime contract is a typed record or virtual registry entry describing the
interface of a provider, event, action, capability, policy, run, checkpoint,
diagnostic, or workflow.

Runtime contracts describe the interfaces used by action handlers, event
sources, watchers, schedulers, agents, and provider APIs supplied by a runtime.

## [#](#explicit-and-implicit-runtime-contracts)Explicit And Implicit Runtime Contracts

Explicit contracts are ordinary Markdown records in a collection or installed
pack.

Implicit contracts are supplied by a conforming runtime, for example built-in
file events or core record actions. A runtime may materialize implicit
contracts as Markdown records for inspection or offline tooling.

# [#](#02-collection-layout)02. Collection Layout

## [#](#identification)Identification

A directory is an mdbase v0.3 collection when it contains `mdbase.yaml` with a
supported v0.3 `spec_version`.

    spec_version: "0.3.0"

Tools MUST NOT treat a parent directory as owning records below a nested
directory that has its own `mdbase.yaml`.

## [#](#recommended-layout)Recommended Layout

    collection/
      mdbase.yaml
      _types/
        meta.md
        task.md
        workflow.md
        action.md
        event.md
      _contracts/
        example.task.md
      actions/
        mdbase.record.patch.md
      events/
        file.created.md
      workflows/
        route-new-file.md
      policies/
        local-runtime-policy.md
      tasks/
        example.md

Only `mdbase.yaml` is required. Untyped records form a valid collection.

View records are ordinary records and require no reserved folder. A collection
MAY organize them under `Views/`, `_views/`, or any other non-excluded path.
Unlike the configured types folder, such a folder remains part of the normal
record scan unless explicitly excluded.

## [#](#reserved-paths)Reserved Paths

The following paths are reserved by default:

- `mdbase.yaml`
- the configured types folder, default `_types/`
- the configured contracts folder, default `_contracts/`
- `.mdbase/` for derived implementation state
- nested collection roots

Runtime folders such as `providers/`, `actions/`, `events/`, `workflows/`, `capabilities/`, `policies/`, `runs/`, and `checkpoints/` contain ordinary
records unless excluded by configuration. Their meaning comes from their type
files and runtime contracts.

## [#](#record-discovery)Record Discovery

Tools discover records by recursively scanning the collection root for files
with configured record extensions. The default extension set is:

    record_extensions: [md]

Tools MUST:

- use forward slash paths in collection APIs
- skip excluded paths
- skip the configured types folder
- skip the configured contracts folder
- skip `.mdbase/`
- stop scanning at nested collection roots
- ignore non-record extensions unless configured otherwise

Tools SHOULD exclude common derived directories by default, including `.git/` and `node_modules/`.

## [#](#type-discovery)Type Discovery

The configured `types_folder` defaults to `_types`.

Every Markdown file directly or recursively under the types folder whose
frontmatter declares `kind: mdbase.type` is a candidate type definition.

Tools MAY warn for files under the types folder that are not valid type files.
They MUST NOT treat type files as data records.

## [#](#data-contract-discovery)Data Contract Discovery

The configured `contracts_folder` defaults to `_contracts`.

Every Markdown file directly or recursively under the contracts folder whose
frontmatter declares `kind: mdbase.contract` is a candidate data contract.
Contract loading, exact-version identity, and implementation validation are
defined in Chapter 05A.

Tools MAY warn for files under the contracts folder that are not valid contract
files. They MUST NOT treat data contract files as records.

## [#](#runtime-record-discovery)Runtime Record Discovery

Runtime records are discovered like ordinary records. A workflow record is a
record whose effective type is `workflow`. An action contract is a record whose
effective type is `action`. The effective type is authoritative; folder names
are conventions.

Tools SHOULD preserve common folder names for portability:

- `actions/`
- `events/`
- `providers/`
- `workflows/`
- `capabilities/`
- `policies/`
- `runs/`
- `checkpoints/`

## [#](#paths-and-safety)Paths And Safety

All collection paths are relative to the collection root and use `/`.

Operations MUST reject paths that escape the collection root after normalization.
This includes `..` traversal, symlink traversal where the implementation follows
symlinks, and absolute paths supplied where a collection-relative path is
required.

Implementations MAY reject platform-reserved filenames or characters when a
write operation targets a filesystem where those paths cannot be represented.

# [#](#03-records-and-frontmatter)03. Records And Frontmatter

## [#](#markdown-record-structure)Markdown Record Structure

A Markdown record may begin with YAML frontmatter delimited by `---` on the
first line:

    ---
    type: task
    title: Fix login
    status: open
    ---

    Body text.

If the first non-byte-order-mark bytes are not `---` followed by a line ending,
the file has no frontmatter and the full file is body text.

Whitespace or a blank line before the opening delimiter means there is no
frontmatter.

## [#](#frontmatter-value)Frontmatter Value

Frontmatter MUST parse to a YAML mapping. Empty frontmatter is an empty mapping.

If frontmatter is absent, the persisted frontmatter object is `{}`.

If frontmatter parses to a scalar, sequence, or other non-mapping value, the
record is invalid at validation level `error`. At validation level `warn`, tools
SHOULD treat it as empty frontmatter and report a warning.

## [#](#missing-null-and-empty)Missing, Null, And Empty

Frontmatter has four distinct states:

- missing means a key is not present in persisted frontmatter
- null means a key is present with YAML null
- empty string means a key is present with `""`
- empty list means a key is present with `[]`

These states are not interchangeable.

`collection.read_defaults` applies only to missing keys. It does not replace
explicit null.

## [#](#persisted-and-effective-frontmatter)Persisted And Effective Frontmatter

`frontmatter` always means the parsed mapping persisted in the Markdown file.
It does not contain read defaults, computed values, or other derived data.

`effective_frontmatter` is the derived read/query mapping after applying `collection.read_defaults` and any other read-time computation supported by the
active profile.

These names have fixed meanings in every operation and provider. An operation
MUST NOT place effective values in `frontmatter`, place persisted values in `effective_frontmatter`, or change either meaning based on request options.

A complete record document has this shape:

    path: tasks/fix-login.md
    revision: sha256:opaque
    types: [task]
    frontmatter:
      title: Fix login
    effective_frontmatter:
      title: Fix login
      status: open
    body: |
      Reproduce and fix the login failure.
    document: |
      ---
      title: Fix login
      ---
      Reproduce and fix the login failure.
    file:
      name: fix-login.md
      folder: tasks
      size: 142
      mtime: 2026-07-26T03:00:00Z

`path`, `revision`, `types`, `frontmatter`, `effective_frontmatter`, `body`, and `file` are all required on a complete record document. Empty frontmatter is
represented by `{}`, not by an absent member.

`document` is an optional complete UTF-8 source representation. It is returned
when an operation explicitly requests source and MUST contain the exact record
text whose bytes produced `revision`, including any byte-order mark, YAML
delimiters, comments, quoting, whitespace, line endings, and trailing newline. `frontmatter` and `body` MUST be parsed from that same text. Providers MUST NOT
reconstruct `document` from parsed frontmatter and body when exact source is
unavailable.

Validation of JSON Schema `required` is against the persisted or draft
frontmatter object, not against effective read defaults.

## [#](#body)Body

The body is the Markdown content after the closing frontmatter delimiter. The
body is not validated by JSON Schema unless a type explicitly models it through
a separate mdbase feature.

The body may participate in queries through `file.body` when body indexing is
enabled or when a tool can read bodies on demand.

## [#](#file-metadata)File Metadata

Every record exposes a file object to expressions and query results:

| Property | Meaning |
| --- | --- |
| `file.path` | collection-relative path |
| `file.name` | basename with extension |
| `file.basename` | basename without the final extension |
| `file.ext` | extension without dot |
| `file.folder` | collection-relative containing folder |
| `file.size` | byte size where available |
| `file.mtime` | modified timestamp where available |
| `file.ctime` | created timestamp where available |
| `file.body` | Markdown body when included or needed for filtering |

File metadata is derived. It MUST NOT be written into frontmatter unless a tool
explicitly maps it to ordinary fields.

## [#](#serialization)Serialization

Write-capable tools SHOULD preserve unrelated body text and line ending style.

When serializing frontmatter, tools SHOULD:

- omit missing values; bare nulls represent explicit null values
- quote empty strings
- preserve array/object structure
- produce deterministic key ordering when the operation rewrites a generated
file

When updating a field to null, tools MAY either persist explicit null or remove
the key depending on operation policy. The operation result MUST make the chosen
behavior explicit.

## [#](#yaml-profile)YAML Profile

Implementations MUST parse UTF-8 Markdown files.

The v0.3 YAML profile SHOULD use a safe YAML parser and MUST NOT execute custom
tags.

Tools SHOULD normalize common YAML scalar forms into the corresponding JSON
data model before JSON Schema validation. Non-JSON YAML values such as NaN,
Infinity, binary values, and timestamps with parser-specific objects MUST be
handled by the mdbase YAML profile before schema validation or rejected with a
clear diagnostic.

# [#](#04-configuration)04. Configuration

## [#](#mdbaseyaml)`mdbase.yaml`

The collection config file is named `mdbase.yaml` and lives at the collection
root.

Minimal v0.3 config:

    spec_version: "0.3.0"

Recommended config:

    spec_version: "0.3.0"

    settings:
      types_folder: _types
      contracts_folder: _contracts
      record_extensions: [md]
      validation: error
      explicit_type_keys: [type, types]
      id_field: id

## [#](#required-keys)Required Keys

`spec_version` is required. During major-zero development, the minor component
is the compatibility boundary. A v0.3 tool MUST reject v0.2 and v0.4
collections unless an explicit compatibility adapter is enabled.

Pre-1.0 draft versions MAY be accepted by explicit compatibility setting.

## [#](#settings)Settings

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `settings.types_folder` | string | `_types` | folder containing type files |
| `settings.contracts_folder` | string | `_contracts` | folder containing data contract files |
| `settings.record_extensions` | list of strings | `[md]` | record file extensions without dot |
| `settings.validation` | string | `error` | default validation level: `off`, `warn`, or `error` |
| `settings.explicit_type_keys` | list of strings | `[type, types]` | frontmatter keys used for explicit type declarations |
| `settings.id_field` | string | `id` | field used for ID-based link and contract resolution |
| `settings.include_subfolders` | boolean | `true` | whether record scanning recurses |
| `settings.exclude` | list of globs | implementation default | excluded paths |

`settings.explicit_type_keys` replaces the default key list. An empty list makes
all type membership inferred.

The types and contracts folders MUST be different normalized paths. Both are
reserved control-file folders and are excluded from ordinary record discovery.

Unknown config keys MUST produce a warning while normal config loading
continues. An explicit strict-config mode MAY reject them.

## [#](#runtime-host-config)Runtime Host Config

Durable-runtime enablement, worker identity, storage, transport binding, and
policy selection are host concerns and are not part of the core `mdbase.yaml` schema. This prevents merely opening a collection from activating executable
behavior.

Portable runtime policies and workflow/state records are ordinary records
implemented by the standard runtime pack. A host MAY preserve private
configuration under an `x-*` extension or in its own settings store, but that
configuration does not change core contract resolution. Runtime profile 0.2
has one resolution model and no contract mode.

## [#](#expressions)Expressions

Portable v0.3 expressions are CEL. No config key is required to opt into CEL.

Tools MAY support non-portable UI expression dialects. Portable stored v0.3
files MUST use the mdbase CEL profile unless a feature declares a different
extension namespace.

View records use CEL for portable filters, projections, selections, and custom
summaries. Compatibility tools MAY read another view or expression format, but
alternate source and round-trip metadata belong under an `x-*` extension and do
not change the meaning of the portable CEL fields.

## [#](#version-compatibility)Version Compatibility

Patch versions within the same stable minor version MUST be backward compatible.

For prerelease v0.3 versions, tools MUST require and report the exact supported identifier
when rejecting a collection.

## [#](#environment-and-includes)Environment And Includes

Config includes and environment substitution are optional local extensions.
Expanded values MUST be the values used for validation and query behavior.
Non-portable config extensions SHOULD use a namespaced key.

# [#](#05-type-files)05. Type Files

## [#](#purpose)Purpose

A type file connects three parts of the mdbase model:

- a rule for selecting records
- a JSON Schema for validating their persisted frontmatter
- collection behavior such as defaults, links, lifecycle assignments, and path
policy
- portable data-contract implementations

The YAML frontmatter is the machine-readable definition. The Markdown body can
document the type for people and tools.

## [#](#minimal-type)Minimal Type

    ---
    kind: mdbase.type
    name: task
    version: 1

    match:
      path_glob: "tasks/**/*.md"

    schema:
      dialect: json-schema-2020-12
      value:
        $schema: "https://json-schema.org/draft/2020-12/schema"
        type: object
        required: [title]
        additionalProperties: false
        properties:
          type:
            const: task
          title:
            type: string
            minLength: 1
    ---

    # Task

    Task records live under `tasks/`.

## [#](#type-evaluation-model)Type Evaluation Model

Type processing has a collection-load phase and a record-evaluation phase.

During collection load, an implementation:

1. discovers candidate type files under the configured types folder
2. validates each file against the built-in type-file schema
3. canonicalizes type names for lookup and detects name conflicts
4. resolves the embedded or referenced JSON Schema
5. validates and compiles that schema against the v0.3 JSON Schema profile
6. resolves and validates every `implements` entry against the local data
contract registry

Each valid definition then enters the collection's type registry. Diagnostics
for an invalid definition identify the type-file path and the failing section.

For each record, an implementation:

1. parses the record path and raw persisted frontmatter
2. determines its matched types using Chapter 07
3. validates the raw frontmatter against every matched JSON Schema
4. evaluates every matched type's collection validators
5. constructs effective read values, including compatible read defaults and
projections supported by the implementation

Type selection and record validation are separate operations. An explicit type
declaration selects a type and still subjects the record to that type's schema
and collection rules.

Write operations use the same model around lifecycle processing: determine
membership from the requested draft, run the applicable lifecycle assignments,
then confirm membership once before final validation. The complete write order
is defined in Chapters 09 and 12.

## [#](#required-frontmatter)Required Frontmatter

A type file MUST include:

- `kind: mdbase.type`
- `name`
- `schema`

`version` SHOULD be present and SHOULD be a positive integer.

## [#](#type-names)Type Names

Type names are stable identifiers. They SHOULD use lower-case ASCII names with
letters, numbers, `_`, and `-`.

Tools MUST compare type names case-insensitively for matching and conflict
detection. Displays SHOULD preserve the casing written by the author.

Two type files whose names differ only by case are conflicting definitions.

## [#](#schema-section)Schema Section

`schema.dialect` identifies the schema profile. v0.3 core defines:

    schema:
      dialect: json-schema-2020-12

The schema can be embedded:

    schema:
      dialect: json-schema-2020-12
      value:
        type: object
        properties:
          title: { type: string }

or referenced:

    schema:
      dialect: json-schema-2020-12
      ref: "../schemas/task.schema.json"

External references resolve relative to the type-file path. Resolution MUST
remain within the collection or installed pack root that owns the schema.

Exactly one of `value` and `ref` is required. A type containing both is invalid.

The JSON Schema validates raw persisted frontmatter. Read defaults,
projections, and lifecycle values enter at the stages defined by their own
chapters.

## [#](#mdbase-sections)mdbase Sections

The following top-level sections are defined by v0.3:

| Section | Purpose |
| --- | --- |
| `match` | select records for inferred type membership |
| `collection` | define Markdown-aware collection semantics |
| `lifecycle` | assign managed values during mutations |
| `runtime` | attach runtime annotations to the type |
| `migrations` | declare explicit type-version migration steps |
| `implements` | declare exact, schema-validated data contract implementations |

Portable type-file validation accepts the core sections and `x-*` extension
sections. Domain and provider metadata belongs under an extension name:

    x-local:
      owner: research-team

This naming rule lets the type-file schema diagnose misspelled core keys such as `collecton`.

Portable interoperability metadata does not belong under `x-*`. It uses the
first-class `implements` section defined in Chapter 05A:

    implements:
      - contract: example.task
        version: 1.0.0
        fields:
          title: title
          status: status

## [#](#meta-type)Meta Type

Implementations MUST have a built-in schema for validating v0.3 type files.

`init` or equivalent tooling SHOULD materialize `_types/meta.md` for inspection.
That file is itself a v0.3 type file matching `_types/**/*.md` and validating
type-file frontmatter against the v0.3 type-file JSON Schema.

The built-in schema is authoritative during bootstrap. A materialized `_types/meta.md` mirrors and documents that behavior.

## [#](#view-type)View Type

Saved views use the ordinary `view` type defined by `schemas/v0.3/view.schema.json`. A collection that stores portable view records
SHOULD materialize `_types/view.md` with `match.where.type: view` and a local
reference to that schema. The repository's `_types/view.md` is the canonical
materialization.

Unlike the meta type, the view type is not required for bootstrap and is not a
built-in control-file category. A view file remains an ordinary Markdown record
and participates in normal reads, validation, links, writes, and type matching.
View-aware execution is the optional behavior defined in Chapter 11.

## [#](#type-membership)Type Membership

Records may select types explicitly or through inferred matching. Chapter 07
defines the decision process, the structured match language, and the optional
CEL Match profile.

Matched type order is deterministic:

- explicit declarations retain declaration order after case-insensitive
de-duplication
- inferred matches are ordered by canonical lower-case type name

## [#](#multiple-matched-types)Multiple Matched Types

If a record matches several types, the implementation validates it independently
against every matched type schema and collection validator. The record is valid
only when all of those validations pass.

Collection behavior composes as follows:

- uniqueness rules are additive and are each evaluated in the type that
declared them
- identical read defaults, link rules, path policies, lifecycle assignments,
and projections coalesce
- different values for the same read-default field, link selector, path policy,
lifecycle event and field, or projection name produce `type_conflict`
- display metadata remains associated with its declaring type; a flattened
display uses the first explicit type or first canonical inferred type

Implementations MUST detect these conflicts before applying the affected
behavior. A write fails before mutation. A read or query reports the conflict
and leaves the conflicted derived value unavailable. The result is independent
of filesystem load order.

For a uniqueness rule with `scope: type`, the comparison set contains every
record that matches the declaring type. A record matching several types
participates independently in each declaring type's uniqueness set.

## [#](#write-time-type-membership)Write-Time Type Membership

Write operations determine membership from the requested draft and target path
before lifecycle runs. Explicit declarations use the same precedence as reads.

After lifecycle has run, the implementation MUST evaluate membership once more.
If the final membership differs from the pre-lifecycle membership, the operation
fails with `type_membership_changed`. Lifecycle is applied once per operation.

An operation that changes membership intentionally expresses that change in its
input patch, target path, or explicit type list.

## [#](#reusable-schema-composition)Reusable Schema Composition

Reusable record shapes use JSON Schema `$defs`, `$ref`, `allOf`, `anyOf`, and `oneOf`. Reusable collection behavior can be generated or supplied by a named
pack or extension.

## [#](#display-metadata)Display Metadata

Human-facing metadata belongs in `collection.display`:

    collection:
      display:
        name_field: title
        icon: check-circle

JSON Schema `title` and `description` remain schema annotations. `collection.display` supplies the collection-specific presentation hints.

# [#](#05a-first-class-contracts)05A. First-Class Contracts

## [#](#why-data-contracts-exist)Why Data Contracts Exist

A contract describes one portable interface independently from the local type
or application that implements it.

For example, `personal_task`, `work_task`, and `task` can all implement `tasknotes.task`. Their filenames, additional fields, matching rules, and local
presentation can differ. An application can discover the shared contract,
understand each type's field mapping, and operate without requiring every
collection to use one canonical type name.

`mdbase.contract` is the shared identity and JSON Schema substrate for passive
record views, events, and actions. `contract_type` discriminates their
subject-specific schema fields. This chapter defines the shared artifact and
record implementation rules. The optional [event/action interoperability profile](#section-05b) defines executable
source/provider declarations and message exchange. Contracts never grant
permission.

## [#](#three-portable-artifacts)Three Portable Artifacts

The complete collection contract model has three intentionally small parts:

1. An `mdbase.contract` artifact defines a versioned subject-specific
interface using JSON Schema 2020-12.
2. For a `record` contract, a type's `implements` entry maps that interface to
the type and supplies contract-specific binding data.
3. An optional `mdbase.type-pack` manifest groups contracts, types, and their
referenced schemas for transactional installation.

Event sources and action providers make runtime declarations because they are
executable, instance-specific implementations rather than record types.
Requirements, authorization grants, and transports are not collection
contract artifacts.

## [#](#designing-record-contracts)Designing Record Contracts

A record contract SHOULD describe a compact, application-facing semantic
interface. Its properties name values that independent consumers can rely on,
while an implementing type remains free to choose local field names, matching
rules, additional fields, and presentation.

Record contracts SHOULD:

- keep the unconditionally required surface as small as the shared behavior
permits
- use optional properties for semantics that not every implementing type can
provide
- use `binding_schema` for implementation-specific vocabularies and behavior,
such as which local task statuses count as completed
- be shared by applications that need the same semantics rather than duplicated
under application-specific IDs

A record contract SHOULD NOT reproduce an external interchange or storage
format merely so applications can request that serialization. An importer,
exporter, or application adapter can translate a semantic record view to
JSContact, vCard, or another wire format. A wire-format-shaped record contract
remains valid when the implementing type intentionally stores and exposes that
exact shape.

Event and action contracts are different: their schemas describe complete
messages at an interoperability boundary, so a transport-shaped schema is
usually appropriate.

## [#](#contract-files)Contract Files

Contract files are Markdown files under the configured contracts folder,
default `_contracts/`. Their frontmatter has `kind: mdbase.contract` and
validates against `schemas/v0.3/data-contract.schema.json`.

    ---
    kind: mdbase.contract
    contract_type: record
    id: example.task
    version: 1.0.0
    name: Example task
    description: A small portable task interface.

    record_schema:
      dialect: json-schema-2020-12
      value:
        $schema: "https://json-schema.org/draft/2020-12/schema"
        type: object
        required: [title, status]
        additionalProperties: false
        properties:
          title: { type: string, minLength: 1 }
          status: { type: string, minLength: 1 }
          due: { type: string, format: date }

    binding_schema:
      dialect: json-schema-2020-12
      value:
        $schema: "https://json-schema.org/draft/2020-12/schema"
        type: object
        required: [completed_values]
        additionalProperties: false
        properties:
          completed_values:
            type: array
            minItems: 1
            uniqueItems: true
            items: { type: string }
    ---

    # Example task

    The body explains the interface to people. Portable behavior is defined by the
    frontmatter schemas.

`id` is a lower-case namespaced identifier. `version` is an exact semantic
version. A type implementation never names a version range.

`record_schema` validates the normalized contract view produced from a record. `binding_schema`, when present, validates implementation-specific semantic
configuration. Both use the same JSON Schema profile and reference rules as
type schemas.

An event contract instead requires `data_schema` and may declare `source_schema`. An action contract requires `input_schema` and may declare `output_schema`, `error_schema`, `provider_schema`, and `behavior`. Subject
fields belonging to another `contract_type` are invalid.

Contract files are control files, not records. They do not participate in
ordinary record scans, queries, links, or runtime workflow discovery.

## [#](#contract-registry)Contract Registry

During collection load, a data-contract-aware implementation:

1. scans the configured contracts folder recursively
2. validates every candidate against the built-in data-contract schema
3. resolves and compiles the schemas selected by `contract_type`
4. registers each contract by the exact pair `(id, version)`
5. computes its contract digest
6. validates every type `implements` entry against the resulting registry

Several versions of one contract ID may coexist. Two artifacts with the same
ID and version are valid only when their contract digests are identical.
Different content for the same ID and version is `data_contract_conflict`.

Resolution is collection-local and offline. Core implementations MUST NOT fetch
a missing contract from the network. Applications and installers carry the
contract files they require, usually in a type pack.

## [#](#type-implementations)Type Implementations

`implements` belongs in the type file because a record implementation is a
claim about each record that matches that one type. A type can implement only a `record` contract. Event sources and action providers declare implementations
through the interoperability profile instead.

    implements:
      - contract: example.task
        version: 1.0.0
        fields:
          title: title
          status: workflow_state
          due: due_date
          "/@type": "/card/@type"
        binding:
          completed_values: [done, cancelled]

Each implementation contains:

| Key | Meaning |
| --- | --- |
| `contract` | exact contract ID |
| `version` | exact contract semantic version |
| `fields` | contract field reference to record field reference mapping |
| `binding` | optional configuration validated by the contract's `binding_schema` |

Both sides of `fields` use the field-reference syntax from Chapter 07. Existing
field paths remain valid. RFC 6901 JSON Pointer is the exact form for keys that
field paths cannot represent, so `/@type` addresses an `@type` property and `/a~1b` addresses an `a/b` property. The left side addresses the normalized
contract view. The right side addresses effective record frontmatter. Mapping
is direct: core does not rename values, coerce values, run expressions, or
apply hidden transforms.

The `binding` object does not transform projected record values. It supplies
validated semantic policy that a contract-aware application can interpret. For
example, a task implementation can expose the local status value unchanged
while declaring several values in `binding.completed_values`. This preserves
the user's vocabulary and avoids inventing an ambiguous reverse mapping.

A type MUST NOT contain two implementations of the same contract ID and
version. Field mappings MUST address fields declared by the resolved `record_schema` and the resolved type schema. Every unconditional top-level
field named by the contract's `record_schema.required` array MUST be mapped,
either by the matching one-segment field path or by the matching one-token JSON
Pointer.

When a contract has a `binding_schema`, the implementation's `binding` value, or
an empty object when omitted, MUST validate against it. When a contract has no `binding_schema`, `binding` MUST be absent or empty.

Private application metadata may remain under `x-*`, but an `x-*` object has no
contract-discovery, conformance, or authorization meaning.

## [#](#contract-views-and-record-validation)Contract Views And Record Validation

To construct a contract view, a tool starts with a record's effective
frontmatter and copies every mapped value to its contract field reference. Missing
optional values remain missing. The resulting object is validated against the
contract's `record_schema`.

Contract validation complements rather than replaces type validation:

- the type schema validates raw persisted frontmatter
- collection semantics construct the effective record
- the field map constructs a normalized contract view
- the contract's `record_schema` validates that view

A record can therefore satisfy its type schema and still produce `data_contract_record_invalid` for one declared implementation. Implementations
MUST surface that diagnostic whenever they expose the record through that
contract.

Static checks at collection load SHOULD diagnose obviously incompatible mapped
schema types early. Runtime contract-view validation remains authoritative when
JSON Schema composition makes static implication impractical.

## [#](#stable-digests)Stable Digests

Digests let a consumer distinguish an approved implementation from a later
change that happens to retain the same name.

The contract digest is SHA-256 over RFC 8785 JSON Canonicalization Scheme bytes
for this object, using the fully resolved JSON Schema values rather than their
storage wrappers or reference paths:

    {
      "kind": "mdbase.contract",
      "contract_type": "record",
      "id": "...",
      "version": "...",
      "record_schema": {},
      "binding_schema": {}
    }

The digest object contains the subject-specific schema keys selected by `contract_type`: `record_schema` and `binding_schema`; `data_schema` and `source_schema`; or `input_schema`, `output_schema`, `error_schema`, `provider_schema`, and `behavior`. Absent optional members are omitted.
Human-facing `name`, `description`, Markdown body, `x-*` metadata, schema
wrapper dialects, and local `ref` paths do not affect portable identity.
Consequently, an inline schema and a local referenced schema with identical
resolved JSON values have the same contract digest, while changing the bytes
at a stable reference path changes the digest.

The implementation digest is SHA-256 over RFC 8785 bytes for:

    {
      "contract_digest": "sha256:...",
      "type": {
        "name": "...",
        "version": 1,
        "match": {},
        "schema": {},
        "collection": {},
        "lifecycle": {}
      },
      "implementation": {}
    }

Absent optional members are omitted. This deliberately includes membership,
shape, defaults, links, paths, and lifecycle behavior. A consumer that pinned
an implementation can detect any portable change that may alter the records or
values it observes.

Digest strings use `sha256:` followed by 64 lower-case hexadecimal characters.

## [#](#multiple-implementations)Multiple Implementations

A contract lookup returns a set of conforming type implementations, never an
arbitrarily selected provider.

Multiple applications may consume the same type implementation. Implementing a
contract does not create an owner, lease, or exclusive provider relationship.

When several types implement one compatible contract requirement:

- read and list experiences SHOULD initially offer their explicit union
- user-facing approval MUST show every included type
- an existing approval MUST pin the exact type names, contract digest, and
implementation digests
- a later implementation MUST NOT silently join that approval
- creation MUST use one explicitly selected implementing type

A product may let a user select a subset instead of the union. It MUST NOT hide
the selection or silently choose the first filesystem entry.

These rules separate interoperability from authorization. A type's `implements` claim says what it can mean; it does not say which application may
read or mutate it.

## [#](#contract-access-and-whole-records)Contract Access And Whole Records

The portable contract view contains only mapped contract fields. A gateway that
grants access "through a contract" MUST expose only that view plus the minimum
record identity needed by its protocol. If a record has several approved views
and the operation does not identify one unambiguously, the gateway MUST require
an explicit contract ID, exact version, and implementing type rather than merge
or guess.

Access to unmapped frontmatter, the Markdown body, or arbitrary records is
whole-record or whole-collection access and MUST be requested and presented
explicitly. Merely implementing a contract never grants either form of access.

Core collection APIs remain authorization-neutral. This distinction is
normative for gateways and application protocols that use data contracts as an
authorization boundary.

## [#](#type-packs)Type Packs

A type pack is a directory or archive with an `mdbase-pack.yaml` manifest that
validates against `schemas/v0.3/type-pack.schema.json`.

    kind: mdbase.type-pack
    id: example.tasks
    version: 1.0.0
    name: Example task types
    resources:
      - kind: contract
        source: contracts/example.task.md
        target: _contracts/example.task.md
        digest: sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
      - kind: type
        source: types/task.md
        target: _types/task.md
        digest: sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789

Resource digests are SHA-256 over the exact resource bytes. Source and target
paths are relative, forward-slash paths without traversal.
A directory, archive, repository, or package that distributes a pack MUST
preserve those bytes exactly, including line endings. Rewriting a text resource
from LF to CRLF therefore creates a different resource and MUST fail digest
verification. This repository fixes text checkouts to LF so its example pack
has identical bytes on every supported platform.

Type packs are installation units, not record types and not permission grants.
A pack may include several contracts, several implementing or auxiliary types,
and local JSON Schemas referenced by those artifacts.

## [#](#transactional-pack-installation)Transactional Pack Installation

A pack-aware installer MUST:

1. validate the manifest, safe paths, source bytes, and resource digests
2. stage every resource without changing the live collection
3. resolve the complete staged contract and type registries
4. validate every contract, implementation, type, reference, and affected
existing record
5. compute and present the exact create, replace, and unchanged diff
6. acquire its collection mutation boundary and recheck overwritten hashes
7. commit all resources as one recoverable transaction
8. reopen the collection and verify the committed registry

An invalid resource aborts before any live write. A conflict or concurrent
change aborts with the live collection unchanged. Implementations may use an
atomic directory exchange or a durable backup journal with rollback. After a
crash, recovery MUST restore either the complete pre-install state or the
complete committed state before normal collection operations resume.

Revoking an application's access does not uninstall its type pack. Uninstall is
a separate, explicitly requested operation because records may still depend on
the installed types.

A pack install or dry-run result reports the pack `id`, exact `version`, and
every resource in manifest order as `{ target, action, digest }`, where `action` is `create`, `replace`, or `unchanged`. It also reports `cleanup_deferred` when committed state is valid but transaction-journal cleanup
must be retried. Reinstalling identical bytes is valid and reports every
resource as `unchanged`; it MUST NOT create a new logical collection revision.

## [#](#diagnostics)Diagnostics

Data-contract-aware tools use these codes:

| Code | Meaning |
| --- | --- |
| `invalid_data_contract` | contract frontmatter or schema is invalid |
| `data_contract_not_found` | an implementation references no local exact contract |
| `data_contract_conflict` | one ID and version resolve to different contract digests |
| `data_contract_version_mismatch` | a consumer requirement has no compatible exact version |
| `data_contract_binding_invalid` | implementation binding fails its binding schema |
| `data_contract_field_invalid` | a mapped contract or record field is missing or incompatible |
| `data_contract_record_invalid` | a projected contract view fails the contract schema |
| `invalid_type_pack` | a pack manifest, resource, path, or digest is invalid |
| `type_pack_conflict` | a target differs from live state and replacement was not approved |
| `type_pack_apply_failed` | transactional commit or recovery did not complete normally |

Diagnostics use the canonical shape from Chapter 16 and identify the contract
ID, exact version, type name, and relevant field mapping in `details`.

# [#](#mdbase-event-and-action-interoperability-profile-01)mdbase Event and Action Interoperability Profile 0.1

## [#](#1-purpose)1. Purpose

This optional profile lets independently developed applications exchange typed
events and invoke typed actions without adopting one workflow engine or durable
runtime.

The profile defines:

- event and action contract kinds built on `mdbase.contract`;
- a CloudEvents 1.0 event envelope;
- action request, admitted invocation, cancellation, and outcome envelopes;
- event-source and action-provider declarations;
- discovery, version resolution, validation, authorization boundaries, and
duplicate semantics;
- role-specific conformance;
- transport capabilities and the baseline bridge behavior.

The profile does not define workflows, schedules, retries, leases, persistence,
or a universal authorization policy.

The profile identifier is `event_action_interop/0.1`. Envelope `profile_version` values use `0.1`; event envelopes carry the CloudEvents
extension `mdbaseprofile: "0.1"`.

The normative schemas are in `schemas/interop/v0.1/`.

## [#](#2-layering)2. Layering

The layers have separate responsibilities:

| Layer | Responsibility |
| --- | --- |
| Contract | identity, exact semantic version, canonical digest, schema, and meaning |
| Interoperability profile | message envelopes, roles, resolution, and boundary validation |
| Transport binding | physical delivery and declared delivery capabilities |
| Application runtime | application-specific behavior |
| Durable runtime | optional workflow state, retries, timers, leases, and recovery |

A contract demonstrates data compatibility. It does not grant authority.
Registration, application installation, and conformance claims do not grant
authority either.

## [#](#3-conformance-roles)3. Conformance Roles

Implementations claim only roles they implement:

| Role | Required behavior |
| --- | --- |
| `event_source` | register exact event artifacts and publish valid events |
| `event_consumer` | subscribe by compatible requirement and validate delivered events |
| `action_caller` | create requests and handle portable outcomes |
| `action_provider` | register exact action artifacts and execute selected invocations |
| `bridge` | resolve, authorize, validate, route, and expose execution evidence |

Transport bindings may define additional roles. A role claim never implies
another role.

## [#](#4-first-class-contract-artifacts)4. First-Class Contract Artifacts

All passive records, events, and actions use the same control-file envelope:

    kind: mdbase.contract
    contract_type: event
    id: tasknotes.task.completed
    version: 1.0.0
    name: Task completed
    description: A TaskNotes task transitioned from incomplete to complete.
    data_schema:
      dialect: json-schema-2020-12
      value:
        type: object
        required: [task_id, completed_at]
        additionalProperties: false
        properties:
          task_id: { type: string, minLength: 1 }
          completed_at: { type: string, format: date-time }

`contract_type` is one of:

- `record`, with `record_schema` and optional `binding_schema`;
- `event`, with `data_schema` and optional `source_schema`;
- `action`, with `input_schema`, optional `output_schema`, optional
`error_schema`, optional `provider_schema`, and optional `behavior`.

Every schema uses the mdbase JSON Schema 2020-12 profile. Event `data`, action `input`, successful action `output`, declared action error details, and
implementation `binding` values validate against the relevant exact artifact.

Record `implements` entries remain in type files. Event sources and action
providers make runtime implementation declarations because their
implementations are executable and instance-specific.

### [#](#41-digest)4.1 Digest

The contract digest is SHA-256 over RFC 8785 canonical JSON containing:

- `kind`, `contract_type`, `id`, and `version`;
- the fully resolved subject-specific schemas;
- action `behavior` when present.

Names, descriptions, Markdown bodies, `x-*` metadata, wrapper dialect keys, and
local schema reference paths do not affect the digest.

The digest input uses the schema field names defined for the selected `contract_type` and omits absent optional fields. A bridge MUST fail with `contract_digest_conflict` when one `(id, version)` pair is presented with
different contract bytes.

## [#](#5-requirements-and-exact-references)5. Requirements and Exact References

An author-facing contract requirement contains:

    { "id": "canvas.card.create", "version": "^1.0.0" }

`version` is a SemVer range. `digest` may additionally pin one artifact.

Execution evidence contains an exact reference:

    {
      "id": "canvas.card.create",
      "version": "1.2.0",
      "digest": "sha256:..."
    }

A bridge resolves a requirement to one exact artifact before event delivery or
action admission. It MUST NOT change a pinned artifact or selected provider
after admission.

## [#](#6-implementation-identity)6. Implementation Identity

Portable implementation identity has four distinct layers:

    application: canvas-bases
    implementation: canvas-bases.obsidian
    version: 1.4.0
    instance_id: vault-01

- `application` names the product or package;
- `implementation` names a concrete implementation line;
- `version` names its build version;
- `instance_id`, when present, distinguishes a live installation.

Declarations and outcomes additionally expose a declaration digest. A
transport binding owns the authenticated principal and authorization evidence.
The portable identity is not itself authentication.

## [#](#7-events)7. Events

### [#](#71-event-contracts-and-source-declarations)7.1 Event Contracts and Source Declarations

An event contract defines the schema and meaning of `data`. It does not name
one owner. Any number of authorized sources may implement the same exact event
contract.

An event-source declaration identifies one implementation, its author-declared
compatible requirement, its exact resolved artifact, optional binding data,
and ordering capabilities. Binding data validates against `source_schema` when
the contract supplies one.

### [#](#72-cloudevents-envelope)7.2 CloudEvents Envelope

Events use CloudEvents 1.0 structured JSON. The standard attributes retain
their CloudEvents meanings:

    {
      "specversion": "1.0",
      "id": "evt_01...",
      "source": "urn:mdbase:app:tasknotes:tasknotes.obsidian",
      "type": "tasknotes.task.completed",
      "time": "2026-07-28T11:30:00.000Z",
      "subject": "urn:mdbase:record:vault-01:task-123",
      "datacontenttype": "application/json",
      "dataschema": "urn:mdbase:contract:tasknotes.task.completed:1.0.0:sha256:...",
      "mdbaseprofile": "0.1",
      "mdbasecontractversion": "1.0.0",
      "mdbasecontractdigest": "sha256:...",
      "mdbaseapplication": "tasknotes",
      "mdbaseimplementation": "tasknotes.obsidian",
      "mdbaseimplementationversion": "5.0.0",
      "correlationid": "flow_01...",
      "causationid": "req_01...",
      "data": {
        "task_id": "task-123",
        "completed_at": "2026-07-28T11:30:00.000Z"
      }
    }

`source` and `id` together identify a logical event across redelivery. `time` is when the domain occurrence happened. Transport receipt time remains
transport metadata. `subject` is an optional stable URI reference.

`type`, `mdbasecontractversion`, and `mdbasecontractdigest` identify the exact
event artifact. `dataschema` is its canonical URN. Bridges MUST reject an
inconsistent combination.

`correlationid` groups related activity. `causationid` identifies the event,
request, or invocation that directly caused this event.

Consumers subscribe by contract ID and compatible version range. Every
authorized matching consumer receives the same logical event. Adding a
consumer does not alter another consumer's subscription.

Events have no global ordering guarantee. Bindings declare `none`, `source`,
or `subject` ordering scopes.

## [#](#8-actions)8. Actions

Events multicast. Actions select exactly one provider.

### [#](#81-action-contracts-and-provider-declarations)8.1 Action Contracts and Provider Declarations

An action contract defines the requested effect and its input, successful
output, declared portable errors, provider binding, and behavior. It does not
name or authorize a provider.

An action-provider declaration identifies:

- the provider implementation;
- a stable handler ID;
- an author-declared compatible contract requirement;
- the exact resolved artifact;
- optional provider binding;
- cancellation and request-deduplication support;
- optional operational limits.

Provider binding validates against `provider_schema`.

### [#](#82-request)8.2 Request

A request is logical caller intent:

    kind: mdbase.action.request
    profile_version: "0.1"
    request_id: req_01...
    contract:
      id: canvas.card.create
      version: ^1.0.0
    caller:
      application: tasknotes-workflows
      implementation: tasknotes-workflows.obsidian
      version: 1.0.0
    created_at: 2026-07-28T11:30:01Z
    correlation_id: flow_01...
    causation_id: evt_01...
    idempotency_key: flow_01:canvas-card
    input:
      canvas: Projects/Report.canvas
      title: Prepare report

An optional requested-provider selector may pin an application,
implementation, or instance. An optional `deadline` is an RFC 3339 timestamp.
An authorization-context reference is bridge supplied and opaque.

The request is not evidence that a provider was selected or that an effect
occurred.

### [#](#83-admitted-invocation)8.3 Admitted Invocation

Before dispatch, a bridge records an admitted invocation containing:

- `request_id`, preserving logical intent;
- a new `invocation_id`;
- an exact contract version and digest;
- the selected provider identity and declaration digest;
- the selected handler ID;
- an `attempt_id`;
- admitted input and trace context;
- admission time and applicable deadline.

The bridge validates the action input and authorization before admission.
Provider selection and the exact contract MUST NOT change after admission.

The base bridge creates one attempt. A durable binding or runtime may create
later attempts but preserves the request and invocation identities and gives
each concrete handler attempt a distinct `attempt_id`.

### [#](#84-outcome)8.4 Outcome

Every terminal result has a distinct `outcome_id` and identifies its request,
invocation, attempt, exact contract, provider, and completion time.

Statuses are:

- `succeeded`;
- `rejected`;
- `failed`;
- `cancelled`;
- `outcome_indeterminate`.

Successful output validates against `output_schema` when present. Every
unsuccessful outcome carries a portable error. A bridge MUST turn an invalid
provider output into `failed` with `invalid_action_output`; it MUST NOT deliver
invalid output as success.

Transport acceptance is not an action outcome.

### [#](#85-selection)8.5 Selection

A bridge:

1. finds exact action artifacts satisfying the request;
2. applies an optional requested-provider selector;
3. removes unauthorized or unavailable providers;
4. selects the one remaining provider; or
5. returns `no_provider`, `requested_provider_unavailable`, or
`ambiguous_provider`.

Providers are never unioned or broadcast. A bridge MUST NOT silently select the
first registered provider. Adding a provider cannot redirect an admitted
request or an explicitly pinned caller.

## [#](#9-validation-and-authorization)9. Validation and Authorization

At every boundary, a bridge validates:

1. the portable envelope;
2. the exact contract identity and digest;
3. source or caller identity attributed by the active binding;
4. implementation declarations and bindings;
5. event data, action input, output, or declared error details;
6. active authorization.

Authorization is separate from compatibility. A bridge may authorize by
principal, role, contract, exact implementation, collection/subject scope,
capability, approval, or resource limit. Credentials and sensitive grants stay
in transport-private state; portable envelopes carry only an opaque reference
when necessary.

Unauthorized publish, subscribe, provider registration, and invocation fail
closed.

## [#](#10-duplicate-idempotency-and-indeterminate-outcomes)10. Duplicate, Idempotency, and Indeterminate Outcomes

The profile does not claim universal exactly-once behavior.

- Consumers deduplicate events by `(source, id)` when duplicate processing is
harmful.
- Requests preserve `request_id` across transport retry.
- An idempotency key is meaningful only when the action contract permits it and
the selected provider declares request deduplication.
- A duplicate request to such a provider returns its recorded terminal outcome
during the declared retention window.
- Without declared deduplication, the bridge rejects a duplicate request rather
than inventing exactly-once behavior.
- If the caller cannot know whether an external effect occurred, the terminal
status is `outcome_indeterminate`.

Retry policy belongs to the caller, binding, or durable runtime.

## [#](#11-cancellation-and-deadlines)11. Cancellation and Deadlines

A caller may request cancellation by `request_id`. Cancellation is
cooperative. The bridge verifies caller ownership and selected-provider
support, then signals the current attempt.

Possible races are represented honestly:

- cancellation before admission returns `cancelled`;
- a cooperatively cancelled attempt returns `cancelled`;
- a completed action keeps its completed outcome;
- an effect whose completion cannot be determined returns
`outcome_indeterminate`;
- a provider without cancellation support returns
`cancellation_unsupported`.

A bridge supporting deadlines rejects admission after the deadline and signals
a cooperative provider when the deadline expires during an attempt. A binding
that cannot enforce a required deadline fails `unsupported_transport_capability`.

## [#](#12-transport-capabilities)12. Transport Capabilities

A binding describes:

- delivery: `ephemeral`, `at_least_once`, `durable_cursor`, `offline_queue`;
- ordering: `none`, `source`, `subject`;
- cancellation and deadline support;
- provider discovery;
- maximum payload size;
- outcome retention;
- request deduplication;
- cross-process identity.

An application that requires an unavailable capability fails clearly before
exchange.

The baseline Obsidian in-process binding is ephemeral, process-local, and
cooperatively attributed. Obsidian does not isolate plugins as security
principals, so this binding cannot defend against a malicious plugin running in
the same process. It still prevents accidental cross-application registration
through opaque client handles and validates host-attributed identities at each
boundary.

## [#](#13-baseline-bridge-api)13. Baseline Bridge API

The behavior-based API is:

    connect one implementation identity
    describe contracts and implementations
    register event source
    publish event
    subscribe to events
    register action provider
    invoke action
    receive action outcome
    cancel action
    dispose client registration

Language SDKs may use idiomatic names. A client handle scopes every
subscription and provider registration to one implementation and removes them
on disposal.

## [#](#14-standard-errors)14. Standard Errors

Portable error codes are defined by `schemas/interop/v0.1/profile.schema.json#/$defs/portableError`.

Transport-private diagnostics may be nested in safe `details`, but they do not
replace the portable category. Error messages MUST NOT expose credentials,
private authorization policy, or provider-internal stack traces.

## [#](#15-relationship-to-runtime-and-connect)15. Relationship to Runtime and Connect

The mdbase runtime profile consumes these contract artifacts and envelopes. It
does not define a second event or action system. Workflow triggers subscribe to
event requirements; workflow steps create action requests; durable dispatch
records the admitted invocation and outcome.

Connect may implement a durable authorized binding using the same envelopes.
Connect control-plane wake-up signals may remain opaque: private event data can
stay at the collection authority and be retrieved only by an authorized
application.

## [#](#16-conformance)16. Conformance

Required scenarios are enumerated in `tests/v0.3/event-action-interop/event-action-interop.yaml`. Claims validate
against `schemas/interop/v0.1/conformance-claim.schema.json`.

The profile is behavior based. Implementations may use any package structure or
programming language.

# [#](#06-json-schema-profile)06. JSON Schema Profile

## [#](#base-dialect)Base Dialect

v0.3 uses JSON Schema 2020-12 for persisted frontmatter shape.

Every embedded schema SHOULD include:

    $schema: "https://json-schema.org/draft/2020-12/schema"

The containing type file MUST declare:

    schema:
      dialect: json-schema-2020-12

## [#](#data-model)Data Model

YAML frontmatter is converted into the JSON data model before schema
validation:

- mapping -> object
- sequence -> array
- string -> string
- integer/float -> number, with integer constraints enforced by JSON Schema
- boolean -> boolean
- null -> null

Values that cannot be represented in the JSON data model MUST be rejected or
normalized by the mdbase YAML profile before JSON Schema validation.

## [#](#required-support)Required Support

Core v0.3 JSON Schema support includes:

- `type`
- `required`
- `properties`
- `additionalProperties`
- `items`
- `enum`
- `const`
- `oneOf`
- `anyOf`
- `allOf`
- `if`, `then`, `else`
- `minimum`, `maximum`
- `exclusiveMinimum`, `exclusiveMaximum`
- `multipleOf`
- `minLength`, `maxLength`
- `pattern`
- `minItems`, `maxItems`, `uniqueItems`
- `$defs`
- local `$ref`
- `title`, `description`, `default`, `examples`, and other annotations

Tools MAY support more of JSON Schema 2020-12. The required profile defines the
portable baseline for Core Read conformance.

## [#](#discriminated-unions)Discriminated Unions

v0.3 uses ordinary JSON Schema constructs for discriminated unions.

JSON Schema validation diagnostics use `schema_<keyword>`, where the JSON
Schema keyword is converted to lower snake_case. For example, `required` produces `schema_required`, `additionalProperties` produces `schema_additional_properties`, and `unevaluatedProperties` produces `schema_unevaluated_properties`. Implementations MUST NOT place camelCase JSON
Schema keyword names into the canonical diagnostic `code` field.

Example:

    type: object
    required: [kind, input]
    oneOf:
      - properties:
          kind: { const: notice.show }
          input:
            type: object
            required: [message]
            properties:
              message: { type: string }
            additionalProperties: false
        required: [kind, input]
      - properties:
          kind: { const: mdbase.record.patch }
          input:
            type: object
            required: [path, patch]
            properties:
              path: { type: string }
              patch: { type: object }
            additionalProperties: false
        required: [kind, input]

Runtime workflow steps normally use `step.action` plus action contracts instead
of embedding every action union directly in the workflow schema.

## [#](#defaults)Defaults

JSON Schema `default` is an annotation. Record mutation occurs through create,
lifecycle, or explicit default materialization, and `required` evaluates the raw
record during validation.

Tools MAY use `default` as a create/editor hint.

Effective read defaults belong in `collection.read_defaults`.

Dynamic defaults such as `now`, `uuid`, `ulid`, and `slugify` belong in `lifecycle`.

## [#](#format)Format

JSON Schema `format` is an annotation in the base dialect. The mdbase v0.3
profile requires assertion behavior for:

- `format: date`
- `format: date-time`
- `format: time`

`date`, `time`, and `date-time` use the RFC 3339 productions referenced by JSON
Schema. `date-time` values MUST contain `Z` or a numeric UTC offset. Invalid
values produce `format_invalid` diagnostics.

Other formats such as `email`, `uri`, and `uuid` MAY be asserted by
implementations. Collections that require them depend on implementation-specific
behavior until a conformance profile defines their semantics.

## [#](#references)References

Tools MUST support fragment-only `$ref` references within an embedded schema.
They MUST also support a type wrapper's `schema.ref` to a local JSON file.

The base URI for `schema.ref` is the directory containing the type file. The
resolved file MUST remain inside the collection root or inside the installed
pack root that supplied the type. Symlinks are resolved before this boundary
check. Escapes produce `schema_ref_forbidden`.

Nested file-to-file `$ref` references use the optional feature identifier `external_schema_refs`. An implementation supporting the feature lists it under `optional_features` in its claim document. Such references use the containing
schema document as their base URI, obey the same owning-root boundary, detect
cycles, and produce `schema_ref_unresolved` or `schema_ref_cycle` diagnostics.
Implementations that do not support the feature report `unsupported_profile` before invoking a resolver.

Core tools MUST NOT fetch network references during collection validation.
Canonical `https://mdbase.dev/schemas/...` identifiers resolve only from the
tool's bundled schema registry. Other HTTP(S) references are `schema_ref_forbidden` unless a non-portable extension explicitly enables them.

An embedded `$id` changes JSON Schema identifier resolution. The owning-root and
network boundaries continue to apply.

## [#](#canonical-schema-identity)Canonical Schema Identity

Normative mdbase schemas have stable `$id` values under:

    https://mdbase.dev/schemas/v0.3/

Runtime profile schemas use their independently versioned namespace:

    https://mdbase.dev/schemas/runtime/v0.1/

Implementations MUST validate against the canonical schema contents associated
with the exact declared spec/profile version. `latest` aliases are unsuitable
for validation.

## [#](#validation-result)Validation Result

JSON Schema validation errors are reported as mdbase validation issues. Each
issue SHOULD include:

- path to the record
- JSON pointer or field path
- code
- severity
- message
- type name
- schema location when available

Tools SHOULD preserve native JSON Schema diagnostic detail when exposing
machine-readable results.

## [#](#additional-properties)Additional Properties

Strictness is expressed with JSON Schema `additionalProperties`.

    additionalProperties: false

A type that persists configured explicit keys such as `type` and `types` MUST
include them in its JSON Schema.

# [#](#07-collection-semantics)07. Collection Semantics

## [#](#purpose)Purpose

`match` selects the records governed by a type. `collection` defines behavior
that depends on the surrounding Markdown collection.

    match:
      path_glob: "tasks/**/*.md"

    collection:
      display:
        name_field: title
      read_defaults:
        status: open
      unique:
        - field: id
          scope: type
      links:
        assignee:
          target_type: person
          validate_exists: true

## [#](#field-references)Field References

Every collection-semantic selector uses one field-reference syntax. A field
reference is either:

- the existing mdbase field-path form, such as `title`, `metadata.owner`, or
`blocks[]`
- a non-root [RFC 6901 JSON Pointer](https://www.rfc-editor.org/rfc/rfc6901),
such as `/@type`, `/metadata/owner`, or `/schema/$id`

Field paths remain supported for compatibility and for the `[]` array-item
selector. JSON Pointer is the exact, standards-based form for every JSON object
key, including keys that contain `.`, `/`, `~`, `@`, or `$`. Pointer tokens
escape `~` as `~0` and `/` as `~1`; for example `/a~1b` selects the key `a/b`.
The URI-fragment form beginning with `#` is not accepted.

The empty JSON Pointer denotes the whole document in RFC 6901, but it is not a
valid mdbase field reference. Collection semantics always address a field
inside the frontmatter object.

JSON Pointer resolves one exact value. Array tokens are zero-based indices. `[]` expansion belongs only to the field-path form. An operation that accepts a
link collection applies its link rule to every item when the resolved value is
an array.

Lifecycle `set` creates missing intermediate objects. It MUST fail rather than
replace a non-object intermediate value. Assignment through an array index is
valid only when that array and index already exist.

## [#](#matching-decision-process)Matching Decision Process

Matching uses the collection-relative record path, raw persisted frontmatter,
the configured explicit type keys, and the loaded type registry.

For each record, an implementation follows this sequence:

1. Inspect the configured explicit type keys in configuration order.
2. If any configured key is present, read and validate its declarations, resolve
those names against the type registry, and use the resulting explicit set.
3. Otherwise, evaluate every type that has a `match` section against the record.
4. Collect the matching types and order them by canonical lower-case name.

The explicit branch completes type selection. Inferred rules, including `match.expr`, are skipped for that record. The selected types still perform
their normal schema and collection validation.

An explicit type value is a type-name string or a non-empty list of type-name
strings. Declarations from several configured keys are concatenated in key order
and case-insensitively de-duplicated while preserving the first occurrence.
Invalid value shapes and unknown type names produce diagnostics.

The default keys are `type` and `types`. Configuring `settings.explicit_type_keys` replaces that default list. An empty list selects
inferred matching for every record.

A type with no `match` section can be selected explicitly. It contributes no
inferred match.

## [#](#inferred-match-rules)Inferred Match Rules

All members present in one `match` object combine with AND:

    match:
      path_glob: "tasks/**/*.md"
      fields_present: [title, "/@type"]
      where:
        status:
          neq: done

This type matches a candidate only when its path, required fields, and
structured predicates all match. A list in `path_glob` combines its patterns
with OR. Every selector in `fields_present` MUST resolve to a raw, non-null
value.

Empty string, `false`, zero, and empty list count as present. A missing value or
explicit `null` does not.

Match rules read persisted frontmatter. Read defaults and projections are
applied after type selection and therefore do not influence inferred matching.

### [#](#structured-predicates)Structured Predicates

`match.where` is the portable structured predicate language for basic matching.
A `where` mapping combines different field selectors with AND. A direct value
uses deep JSON equality. An operator mapping combines its operators with AND.

| Operator | Meaning |
| --- | --- |
| `eq`, `neq` | deep JSON equality or inequality |
| `gt`, `gte`, `lt`, `lte` | number, string, date, time, or date-time comparison |
| `contains` | string substring or array item equality |
| `containsAll`, `containsAny` | array contains all or any requested values |
| `startsWith`, `endsWith` | string prefix or suffix |
| `matches` | portable regular-expression match |
| `exists` | raw key presence, including an explicit null value |

Except for `exists`, an operator evaluates to false when its field is missing or
null. Ordering evaluates to false for incomparable values. `neq` also evaluates
to false for a missing field. A predicate with an operand of the wrong type
evaluates to false.

`matches` uses the same portable regular-expression subset as JSON Schema `pattern`: Unicode-aware matching without backreferences or look-around. An
unsupported or invalid pattern is a type-file diagnostic.

### [#](#cel-matching)CEL Matching

The CEL Match profile adds `match.expr` for inferred rules that need a portable
expression:

    match:
      path_glob: "tasks/**/*.md"
      expr:
        $expr: 'present.raw.tags && tags.exists(t, t == "task")'

The expression combines with the other members of `match` using AND. It receives
the matching context defined in Chapter 10 and MUST evaluate to boolean true for
the type to match.

Implementations compile `match.expr` when loading the type. Parse and type
errors invalidate the type definition. A per-record evaluation error reports a
diagnostic for that record and expression and yields a non-match. False and null
also yield a non-match.

A type containing `match.expr` requires the `cel_match` conformance profile.
Loading it under a claim that omits `cel_match` produces `unsupported_profile`.

## [#](#read-defaults)Read Defaults

`collection.read_defaults` supplies effective read and query values for missing
fields.

    collection:
      read_defaults:
        status: open
        recurrenceAnchor: scheduled

For each defaulted field:

- a missing raw field receives the configured effective value
- an explicit `null` remains null
- JSON Schema `required` continues to evaluate the raw frontmatter
- raw-frontmatter presence remains false
- read and query operations leave the persisted file unchanged

Create and editor tooling MAY mirror static values into JSON Schema `default` annotations for presentation or scaffolding.

## [#](#links)Links

JSON Schema validates a link field's local shape. `collection.links` supplies its collection-level meaning.

    collection:
      links:
        parent:
          target_type: task
          validate_exists: true
        blocks[]:
          target_type: task
          validate_exists: false
        "/relations":
          target_type: task
          validate_exists: true

`blocks[]` applies the rule to every item in the `blocks` array. Chapter 08
defines link parsing and resolution. `/relations` applies the same item-wise
rule when the exactly selected value is an array.

## [#](#cross-file-uniqueness)Cross-File Uniqueness

`collection.unique` declares collection-level uniqueness:

    collection:
      unique:
        - field: id
          scope: type

| Scope | Comparison set |
| --- | --- |
| `collection` | every record in the collection |
| `type` | every record matching the declaring type |
| `path_glob` | every record under the configured path glob |

Missing and null values are exempt from uniqueness comparison.

## [#](#path-policy)Path Policy

Path policy guides create and rename operations:

    collection:
      path:
        pattern: "tasks/{id}.md"

The portable grammar uses `{field}` placeholders for top-level frontmatter
fields. Values are converted to strings without expression evaluation. A
missing or null value produces `path_value_missing`.

Generated paths MUST remain inside the collection root. Placeholder values that
produce `/`, `\\`, `.`, or `..` path components are invalid. Runtime-owned path
logic and richer template languages belong under an `x-*` extension.

## [#](#display-metadata)Display Metadata

`collection.display` contains advisory presentation metadata:

    collection:
      display:
        name_field: title
        description_field: summary
        icon: check-circle
        color_field: status

Editors, collection views, and generated documentation can use these values.
Record validation ignores display metadata.

## [#](#projections)Projections

Collection projections are an optional effective-value feature declared outside
JSON Schema and expressed in CEL:

    collection:
      projections:
        is_overdue:
          expr: 'present.record.due && due < today() && status != "done"'

Projection values are available to queries. Persistence occurs only through an
explicit write operation or runtime workflow.

Collection projections enter the effective record and remain available through
their declared field names. Query- and view-local named projections are
separate, live under the `projection` CEL namespace, and never replace a
collection projection or persisted field with the same name.

## [#](#private-domain-namespaces)Private Domain Namespaces

Private annotations with no portable interoperability meaning use namespaced
extension sections:

    x-example-app:
      fields:
        status:
          role: status
          completed_values: [done, cancelled]

This keeps the JSON Schema reusable and gives each private extension an
explicit owner. Portable domain interfaces use the first-class data contracts
and type-local `implements` entries from Chapter 05A.

# [#](#08-links)08. Links

## [#](#link-values)Link Values

mdbase recognizes three link syntaxes:

| Syntax | Example |
| --- | --- |
| Wikilink | `[[people/alice |
| Markdown link | `[Alice](people/alice.md)` |
| Bare path | `people/alice.md` |

`collection.links` makes a field link-aware. JSON Schema validates its local
shape, usually `string` or an array of strings.

## [#](#link-components)Link Components

Parsed links expose:

- `raw`
- `target`
- `alias`
- `anchor`
- `format`
- `is_relative`

`format` is one of `wikilink`, `markdown`, or `path`.

## [#](#resolution)Resolution

Resolution is performed against the collection root and the containing file.

General rules:

- Markdown links and bare relative paths resolve relative to the containing
file's folder.
- Absolute collection paths use `/` from the collection root.
- Wikilinks with `/`, `./`, or `../` use path-style resolution.
- Simple wikilinks without path separators first try ID-based resolution using
the configured `id_field`, then filename resolution.

After normalization, a link that escapes the collection root is invalid.

## [#](#ambiguity)Ambiguity

If multiple records have the same configured ID, ID-based resolution is
ambiguous and MUST fail.

If filename resolution finds multiple candidates, tools SHOULD apply stable
tiebreakers:

1. same directory as referring file
2. shortest collection path
3. alphabetical path

If ambiguity remains, resolution returns null and reports an ambiguous link
warning.

## [#](#target-constraints)Target Constraints

`collection.links` can require a target type:

    collection:
      links:
        assignee:
          target_type: person
          validate_exists: true

When `validate_exists` is true, unresolved links are validation errors at
validation level `error`.

When `target_type` is present, a resolved target is valid only if it matches the
target type.

## [#](#body-links)Body Links

`file.links` includes:

- frontmatter link fields declared in `collection.links`
- body wikilinks
- body Markdown links

`file.embeds` includes Markdown and wikilink embeds.

Links and tags inside fenced code blocks and inline code MUST be ignored by
body extraction.

## [#](#tags)Tags

`file.tags` includes:

- frontmatter tags from `tags` when the field is string or list of strings
- inline body tags beginning with `#`

Inline tags must begin at the start of a line or after whitespace. URL
fragments MUST NOT be treated as tags.

`file.hasTag("project")` uses complete tag-segment prefixes. It matches `#project` and `#project/alpha`; its result for `#projection` is false.

## [#](#link-host-functions)Link Host Functions

The CEL profile defines host functions and methods for links:

- `link(value)`
- `file.hasLink(linkValue)`
- `file.hasTag(tag)`
- `file.asLink()`
- `linkValue.asFile()`

Broken links resolve to null. Property access through null returns null under
the mdbase CEL profile.

## [#](#round-trip)Round Trip

Write operations SHOULD preserve link format when updating references:

- wikilinks remain wikilinks
- Markdown links remain Markdown links
- bare paths remain bare paths
- aliases and anchors are preserved where possible

ID-based links SHOULD NOT be rewritten during rename if the target ID did not
change.

# [#](#09-lifecycle)09. Lifecycle

## [#](#purpose)Purpose

`lifecycle` defines deterministic mutation-time behavior for managed fields.

Lifecycle is part of Core Write behavior and runs independently of the workflow
runtime.

## [#](#events)Events

Lifecycle policy may run on:

- `on_create`
- `on_update`
- `on_delete`
- `on_rename`

v0.3 core standardizes `on_create` and `on_update`. Other lifecycle hooks are
optional until a profile defines them.

## [#](#example)Example

    lifecycle:
      on_create:
        set:
          id: { ulid: true }
          dateCreated: { now: true }
          dateModified: { now: true }
      on_update:
        set:
          dateModified: { now: true }

## [#](#standard-value-providers)Standard Value Providers

Core lifecycle providers:

| Provider | Meaning |
| --- | --- |
| `{ now: true }` | current timestamp |
| `{ today: true }` | current date |
| `{ uuid: true }` | random UUID |
| `{ ulid: true }` | random ULID |
| `{ slugify: fieldName }` | slugified value of another field |
| `{ copy: fieldName }` | copy another field |
| `{ literal: value }` | set a literal value |

Tools MUST document timestamp precision and timezone behavior.

## [#](#guards)Guards

Lifecycle actions MAY have CEL guards:

    lifecycle:
      on_update:
        - if: 'old.status != status'
          set:
            dateModified: { now: true }

Lifecycle guards use the mdbase CEL profile with these bindings:

- current draft frontmatter fields
- `old` for the previous raw frontmatter on update
- `file` for file metadata
- `operation` for operation metadata

## [#](#validation-order)Validation Order

For mutating operations:

1. parse input
2. match target types
3. build a draft frontmatter object
4. apply lifecycle policy
5. validate JSON Schema
6. run collection validators
7. write the file

Lifecycle MUST run before final validation so generated IDs and timestamps can
satisfy required schema fields.

Read defaults MUST NOT run as lifecycle policy unless a write operation
explicitly asks to materialize them.

## [#](#relationship-to-workflows)Relationship To Workflows

Lifecycle is deterministic, local, and operation-scoped.

Workflows are event/action orchestration. They may read or write records, call
agents, request approvals, run commands, or produce run state.

Generated IDs and timestamps belong in lifecycle. Cross-record automation and
agent work belong in workflows.

## [#](#conflicts)Conflicts

If multiple matched types define lifecycle policy for the same field and event,
the operation MUST be deterministic.

Normative rule:

- identical normalized assignments are allowed and execute once
- conflicting assignments are `type_conflict` errors before write
- diagnostics MUST report the type names and lifecycle paths involved

v0.3 core MUST report the conflict. Future pack composition may define explicit
precedence.

A lifecycle guard that fails to compile or raises an evaluation error fails the
operation with `lifecycle_expression_error`. A guard that evaluates normally to
false or null skips that action.

# [#](#10-cel-profile)10. CEL Profile

## [#](#purpose)Purpose

Portable v0.3 expressions use CEL. This chapter defines the data available to an
expression, mdbase host functions, missing-value behavior, limits, and error
handling.

The base `cel` conformance profile covers this shared expression model. Features
that embed CEL add their own context requirements: `cel_match`, `cel_query`,
Lifecycle, and Workflow.

Each containing object defines how source text is stored. Query `where` uses a
string, while `match.expr` and workflow expression values use an object such as:

    $expr: 'status == "open"'

Plain strings in workflow inputs remain literal strings.

## [#](#expression-locations)Expression Locations

CEL appears in:

- `match.expr`
- query filters and projections
- collection projections
- lifecycle guards
- workflow variables, conditions, inputs, iteration, and run policy

`match.where` uses the structured predicate language from Chapter 07.

Tools may translate another user-interface expression language to CEL before
writing portable records. A stored alternate dialect uses an `x-*` extension
whose owner defines its semantics.

## [#](#evaluation-contexts)Evaluation Contexts

Every expression location has a context contract. A host supplies exactly the
system bindings listed for that context, along with the applicable top-level
record fields.

| Context | Available values |
| --- | --- |
| inferred match | candidate raw fields at top level; `record`, `raw`, `present`, `file`, `note` |
| query or projection | effective candidate fields at top level; `record`, `raw`, `present`, `file`, `note`; `projection`; `this` as the invocation-context record or null |
| query summary | `values`; the fixed operation time and timezone |
| lifecycle guard | current draft fields at top level; `record`, `raw`, `present`, `old`, `file`, `operation` |
| workflow variable, trigger, or workflow condition | `event`, `workflow`, `trigger`, `vars` |
| workflow step condition or input | `event`, `workflow`, `trigger`, `steps`, `vars`; `item` during iteration |
| workflow run-policy expression | `event`, `workflow`, `trigger`, `vars` |

An unavailable system binding is a compile or preflight diagnostic. For example, `steps` is unavailable to a trigger condition because no step has run.

The system names `record`, `raw`, `present`, `file`, `note`, `projection`, `this`, `values`, `old`, `operation`, `event`, `workflow`, `trigger`, `steps`, `vars`, and `item` are reserved. A frontmatter field with one of those names
remains available through `record.<field>` and `raw.<field>`.

### [#](#query-context)Query Context

In a query, top-level fields and `record` contain effective values. `raw` contains persisted frontmatter. `note` is an alias for `record`.

    present.raw.status == false && record.status == "open"

`file` supplies the candidate metadata and helpers defined by the collection,
query, and link profiles. `projection` contains named query projections after
their dependency-ordered evaluation.

`this` is reserved for the query invocation-context record. It is null when no
context is bound. A non-null context mirrors the candidate query namespaces:

- `this.<field>`, `this.record.<field>`, and `this.note.<field>` expose
effective context values
- `this.raw.<field>` exposes persisted context frontmatter
- `this.present.record.<field>` and `this.present.raw.<field>` expose presence
- `this.file` exposes context-file metadata and the helpers available to the
active profiles

When an effective context field conflicts with the reserved members `record`, `note`, `raw`, `present`, or `file`, it remains available through `this.record.<field>` and `this.raw.<field>`.

The host resolves and snapshots the context once before evaluating candidates.
All candidates see the same immutable context, operation time, and timezone.
Context link values and `this.file` helpers resolve relative to the context
record; candidate values and `file` helpers resolve relative to the candidate. `this` is record-only and MUST NOT be repurposed as an arbitrary caller
parameter map. A feature that adds parameters uses a distinct binding and
declares its own context contract.
Chapter 11 defines portable context binding and saved-view invocation.

### [#](#matching-context)Matching Context

In `match.expr`, the candidate has not yet acquired a type. `record`, `raw`, `note`, and top-level field names therefore refer to the same raw frontmatter
object. `present.record` and `present.raw` contain the same values.

    file.inFolder("tasks") && present.raw.tags && tags.exists(t, t == "task")

Read defaults and projections enter after matching and are absent from this
context.

### [#](#lifecycle-context)Lifecycle Context

Lifecycle expressions evaluate against the current write draft. Top-level
fields, `record`, and `raw` contain that draft. On update, `old` contains the
previous raw frontmatter. `operation` describes the active create or update and `file` describes its target path and available metadata.

    old.status != status

### [#](#workflow-context)Workflow Context

Workflow expressions use the validated event envelope and workflow metadata.
Trigger and run-policy expressions execute before steps. Step expressions also
receive the standard results of completed steps. Iteration adds the current
item under the configured iteration name, with `item` as the default.

    event.data.zone.id

    steps.patch_status.output.path

Chapter 14 defines when each workflow expression is evaluated.

## [#](#missing-and-null)Missing And Null

mdbase preserves four observable record states:

| Raw state | Effective state | `present.raw` | `present.record` | Top-level query value |
| --- | --- | --- | --- | --- |
| missing, no default | missing | `false` | `false` | `null` |
| missing, read default | default value | `false` | `true` | default value |
| explicit null | null | `true` | `true` | `null` |
| persisted value | persisted value | `true` | `true` | persisted value |

Hosts MUST make missing record fields evaluate to null and presence entries
evaluate to false. `present` includes schema-known, persisted, and defaulted field
names so these checks do not depend on host map-access behavior.

Property access through null returns null. Type errors in query evaluation also
yield null and produce an expression diagnostic; a query filter includes only a
record whose condition evaluates to boolean true.

Portable frontmatter-presence checks use `present.raw.<field>`. `present.record.<field>` tests effective presence. CEL's `has()` macro remains
available for objects whose host representation has ordinary CEL presence
semantics, such as event payload objects.

## [#](#date-and-duration)Date And Duration

The profile defines these host functions:

- `now()`
- `today()`
- `duration(string)`

`now()` returns an RFC 3339 UTC date-time. `today()` returns the calendar date in
the runtime's declared timezone. The operation or runtime context MUST declare
that timezone. `duration(string)` accepts ISO 8601 durations in portable stored
expressions.

Timestamp comparison uses the represented instant. Date comparison uses the
calendar date. Date, time, timestamp, and duration values have stable
serialization across reads and query results.

## [#](#file-and-link-helpers)File And Link Helpers

Core Read supplies file metadata and:

- `file.inFolder(path)`

The Links profile adds:

- `link(value)`
- `file.hasTag(tag)`
- `file.hasLink(linkValue)`
- `file.asLink()`
- `linkValue.asFile()`

`asFile()` returns null for an unresolved link. Traversal depth is bounded as
described below. Expressions can use helpers supplied by every profile in the
implementation's conformance claim.

## [#](#limits)Limits

Implementations MUST bound expression source size, AST depth, evaluation work,
and link traversal. The portable minimum supported limits are:

| Limit | Minimum supported value |
| --- | --- |
| expression source | 64 KiB |
| AST depth | 100 |
| link traversal depth | 10 |

Hosts SHOULD also bound list iteration, elapsed evaluation time, and memory.
Operational limits are reported in conformance claims. Exceeding a limit
produces a diagnostic identifying the limit and configured value.

## [#](#compilation-and-evaluation-errors)Compilation And Evaluation Errors

All stored expressions MUST compile during preflight of their containing type,
query, lifecycle policy, workflow, or runtime object. Parse and type errors
invalidate that containing object.

Evaluation outcomes depend on context:

| Context | Evaluation error |
| --- | --- |
| inferred match | report a diagnostic and treat the candidate as a non-match |
| query filter or projection | yield null for that record and report a diagnostic |
| lifecycle guard | fail the write operation with `lifecycle_expression_error` |
| workflow trigger or workflow condition | create a failed-run diagnostic |
| workflow step or run policy | fail the step or run with a runtime diagnostic |

A condition that evaluates normally to false or null does not match or execute.

Diagnostics SHOULD include the source, code, message, context name, and line,
column, or byte range when available. Parse/type diagnostics and runtime
evaluation diagnostics use distinct codes.

# [#](#11-querying)11. Querying

## [#](#query-object)Query Object

A query selects records from a collection. The canonical query-object schema is `schemas/v0.3/query.schema.json`.

    types: [task]
    context:
      this:
        path: projects/alpha.md
    projections:
      is_overdue:
        expr: 'present.record.due && due < today() && status != "done"'
    where: 'status != "done" && priority >= 3'
    select:
      - title
      - due
      - projection.is_overdue
    order_by:
      - field: due
        direction: asc
    limit: 20
    offset: 0
    include_body: false

Unknown query members are invalid. `x-*` members MAY carry adapter metadata but
MUST NOT change the meaning of portable members.

Schema violations and semantic preflight failures such as cyclic projection
dependencies or duplicate result names produce `invalid_query` and abort before
candidate evaluation.

## [#](#types)Types

`types` is an OR filter. A record is included if it matches at least one listed
type.

If `types` is omitted, all records are candidates.

## [#](#invocation-context-and-this)Invocation Context And `this`

A query MAY bind one collection record as its invocation context:

    context:
      this:
        path: projects/alpha.md

The path is collection-relative and MUST resolve to an ordinary record in the
same collection. The implementation reads the context through the same raw and
effective-value pipeline as any other record, snapshots it once before
candidate evaluation, and binds it to `this` as defined in Chapter 10.

When no context is supplied, `this` is null. Supplying an unresolved context
produces `context_not_found` and aborts the query before candidate evaluation.
An invalid context is handled according to the collection validation level.

Query time, timezone, and collection state are fixed for the context and all
candidates in one execution. A caller MUST NOT replace or mutate the context
between candidate evaluations.

## [#](#named-projections)Named Projections

Queries MAY define named projections evaluated for every candidate before the
filter:

    projections:
      is_overdue:
        expr: 'present.record.due && due < today() && status != "done"'
      urgency:
        expr: 'priority + (projection.is_overdue ? 10 : 0)'

Projection names are available under `projection.<name>` in subsequent
projection expressions, `where`, selection expressions, ordering, grouping,
summaries, and presentation mappings.

Implementations MUST resolve projection dependencies deterministically and
reject direct or indirect cycles before evaluating candidates. A projection
evaluation error produces null for that candidate and a per-record diagnostic;
it does not abort evaluation of other candidates.

Named query projections are effective query values. They are not persisted and
do not replace raw or effective frontmatter fields with the same name.

## [#](#where)Where

`where` is a CEL expression evaluated against the effective candidate and its
named projections. The expression result includes the record only when it is
boolean true.

Evaluation errors MUST produce null for that record, exclude it, and report a
diagnostic according to query options.

## [#](#selection)Selection

Queries MAY request a logical result shape:

    select:
      - title
      - file.path
      - projection.is_overdue
      - name: display_due
        expr: 'due == null ? "Unscheduled" : string(due)'
        label: Due

A string selects an effective field, file value, or named projection. An object
defines a named CEL result value. Selection expressions run after filtering and
may use the candidate, named projections, and `this`.

For a string selector, the output name is the effective-field name or the final
member of a `file.<name>` or `projection.<name>` selector. An object uses its
required `name`. Two selections that produce the same output name make the
query invalid; callers use an object selection to assign an unambiguous name.

Computed selection values belong to the query result. Persistence requires an
explicit write operation. Every result includes `file.path` in its `file` object even when `file.path` is not selected.

## [#](#ordering)Ordering

`order_by` sorts by one or more effective fields, file values, named
projections, or named selection values.

    order_by:
      - field: due
        direction: asc
      - field: projection.urgency
        direction: desc

Null values sort last in ascending order and first in descending order.

When all ordering fields compare equal, tools MUST tie-break by ascending `file.path` for deterministic results.

## [#](#grouping-and-summaries)Grouping And Summaries

Queries MAY group the complete filtered and ordered result set by one or more
values:

    group_by:
      - field: status
        direction: asc

Each distinct ordered tuple of grouping values creates one group. Null and
missing values form the same null group. Group tuple ordering applies the same
direction and null rules as `order_by`. Results within a group retain query
order.

Queries MAY define custom summary functions and apply built-in or custom
functions to values:

    summary_functions:
      completion_rate:
        expr: 'values.size() == 0 ? 0 : values.filter(v, v).size() * 100 / values.size()'

    summaries:
      - field: estimate
        function: sum
        name: total_estimate
      - field: projection.is_completed
        function: completion_rate
        name: completion_rate

A custom summary expression receives `values`, an ordered list containing one
value per matching result in result order. Missing fields contribute null.
When grouping is active, the function is evaluated independently for each
group; otherwise it is evaluated once for the complete match set.

A summary's result key is its explicit `name`, or its `function` identifier when `name` is omitted. Duplicate result keys make the query invalid.

The portable built-in summary identifiers are `count`, `sum`, `average`, `minimum`, `maximum`, `earliest`, `latest`, `empty`, and `filled`. `count` counts all values. `empty` and `filled` count empty and non-empty values using
the CEL missing/null contract. Other built-ins ignore null values and produce
null when no compatible non-null value exists. Incompatible non-null values
produce a summary diagnostic and a null summary result.

Grouping and summaries are calculated before pagination and therefore describe
the complete filtered match set. Group metadata does not replace the flat `results` page.

## [#](#pagination)Pagination

`limit` and `offset` apply after filtering and sorting.

`limit: 0` returns an empty result page. Total count, grouping, and summary
metadata still describe the complete match set.

## [#](#body-search)Body Search

`file.body` MAY be used in filters even when `include_body` is false. In that
case the body is available for filtering but not returned in results.

Tools MAY report that body filtering requires a profile or index when they
cannot read bodies on demand.

## [#](#frontmatter-mode)Frontmatter Mode

`frontmatter_mode` controls which fixed-semantics frontmatter members appear in
each result:

- `effective` (the default) returns `effective_frontmatter`
- `persisted` returns `frontmatter`
- `both` returns both `frontmatter` and `effective_frontmatter`

The mode changes result serialization only. Filtering, projections, selection,
ordering, grouping, and summaries continue to use effective values unless an
expression explicitly reads the persisted namespace. Unlike a complete record
document returned by Core Read or a mutation, a query result is a summary and
MAY omit the member excluded by the requested mode.

## [#](#result-envelope)Result Envelope

Query results MUST use this envelope:

    results:
      - file:
          path: tasks/fix-login.md
        effective_frontmatter:
          title: Fix login
          status: open
        values:
          title: Fix login
          is_overdue: true
    meta:
      total_count: 1
      has_more: false
      context:
        path: projects/alpha.md
      groups:
        - values:
            status: open
          count: 1
          summaries:
            total_estimate: 30
    diagnostics: []

Each result MUST include `file.path`. `meta.total_count` is the count before
pagination, and `meta.has_more` is true when additional matching records remain.
Diagnostics use the canonical diagnostic envelope from the conformance chapter.

Read defaults are included in `effective_frontmatter`. `values` contains
requested selection values and is omitted when `select` is omitted.

`meta.context.path` identifies the bound invocation context and is omitted when `this` is null. `meta.groups` is present only when grouping or summaries were
requested. Without grouping, summaries appear in one group whose `values` is an
empty object.

## [#](#saved-view-records)Saved View Records

A saved view is an ordinary Markdown record matched by the `view` type. The
canonical query object above supplies its execution semantics, and the view
record provides a portable persisted container. The canonical record schema is `schemas/v0.3/view.schema.json`; a collection can materialize the corresponding `_types/view.md` type file.

One view record contains a shared canonical query fragment and one or more
named views. The shared fragment is nested under `query` so its `types` member
cannot be mistaken for the record-level explicit type declaration:

    ---
    type: view
    id: tasknotes.tasks
    version: 1
    name: Task views

    query:
      types: [task]
      projections:
        urgency:
          expr: 'priority + (due < today() ? 10 : 0)'

    views:
      - id: today
        name: Today
        where: 'due == today() || scheduled == today()'
        select: [title, due, projection.urgency]
        order_by:
          - field: projection.urgency
            direction: desc
        presentation:
          type: tasknotes.task-list
          fallback: mdbase.table
    ---

    # Task views

    Reusable task views for editors, CLIs, and agents.

### [#](#named-view-resolution)Named-view resolution

A named view is addressed by the view record path or stable record ID plus the
named-view ID. Human-readable names are not identifiers. Duplicate named-view
IDs make the view record invalid.

To derive an executable query:

1. inherit `query.types` unless the named view supplies `types`
2. combine `query.where` and named-view `where` with AND
3. merge `query.projections` and named-view projections; the same name may appear in both
only when its parsed definition is structurally equal, ignoring mapping key
order
4. inherit `query.context` unless the named view supplies context
5. copy the named view's selection, ordering, grouping, summaries, pagination,
and body options
6. bind the invocation context using the rules below

Record-level property metadata and summary functions remain available to every
named view. Resolving an unknown view record or named-view ID produces `view_not_found`.

View execution returns the query envelope and adds `meta.view: { path, id }`, using the resolved view-record path and named-view
ID.

Property-metadata keys MAY name effective fields, `file.*` values, `projection.*` values, or selection outputs. They provide labels, descriptions,
format hints, and visibility hints. `select` remains the source of result
values.

### [#](#view-invocation-context)View invocation context

View records declare what to do when the caller supplies no context:

    query:
      context:
        this:
          on_missing: view
          types: [project]

`on_missing` values are:

| Value | Behavior |
| --- | --- |
| `view` | bind the view-definition record; default |
| `null` | bind `this` to null |
| `error` | abort with `context_required` |

An explicitly supplied context always wins. If `types` is present, a non-null
context must match at least one listed type or execution aborts with `context_type_mismatch`. A named-view context declaration replaces, rather than
partially merges with, the shared `query.context` declaration.

An embedding host supplies the embedding record. A headless caller supplies a
collection-relative record path. An editor may map an active record to the
explicit invocation context, but ambient concepts such as workspace leaves or
sidebars are not part of portable execution.

### [#](#presentation)Presentation

`presentation.type` and `presentation.fallback` are open renderer identifiers,
not a closed enumeration. `select` defines the portable logical result shape;
presentation mappings and options describe how a supporting tool may render
that result.

Each `presentation.mappings` key is a renderer-defined role and its value names
an output produced by `select`. Selected named projections are therefore
available to presentation without giving presentation its own projection
language. A named view with no `presentation` is a complete headless saved
query.

Saved-view discovery exposes the selected outputs as ordered property
descriptors. A renderer uses those descriptors to preserve selection order and
labels while reading the corresponding values from each result row.

Presentation metadata is advisory. Filtering, projection, ordering, grouping,
summaries, pagination, and the headless result envelope retain their canonical
query semantics. Headless execution succeeds for every renderer identifier. A
request for rendered output reports `unsupported_presentation` when neither the
requested renderer nor its declared fallback is available.

Source syntax, renderer configuration, and round-trip data use `x-*` extensions. Chapter 15 defines the adapter contract for Obsidian Bases sources.

### [#](#optional-support)Optional support

Core Read implementations treat a canonical view file as an ordinary typed
record. A tool advertises `view_records` in its `optional_features` claim when
it resolves, lists, and executes named views with the semantics in this
chapter.

# [#](#12-operations)12. Operations

## [#](#operation-model)Operation Model

Write-capable tools operate on Markdown files while preserving the collection
contract.

Core operations:

- read
- create
- update
- delete
- rename
- batch

Optional saved-view operations:

- list_views
- execute_view
- read_view_source
- create_view_source
- update_view_source
- delete_view_source

## [#](#read)Read

Read returns a record by path, including:

- persisted `frontmatter`
- derived `effective_frontmatter`
- body
- file metadata
- matched type names
- diagnostics

Read MUST NOT write defaults or lifecycle values to disk.
Every successful read returns the complete record document defined in Chapter
03.

Read accepts optional `include_document`. When true, the returned record MUST
include the exact `document` source defined in Chapter 03. Providers that
cannot supply exact source MUST reject the request rather than silently return
a reconstructed serialization.

## [#](#create)Create

Create builds a new record.

Pipeline:

1. determine target type or types from explicit input or path/match policy
2. build draft frontmatter
3. freeze pre-lifecycle type membership
4. apply lifecycle `on_create`
5. verify type membership did not change as a lifecycle side effect
6. validate JSON Schema
7. run collection validators
8. choose or validate path policy
9. write Markdown file
10. update derived indexes
11. emit watch/runtime events after state is consistent

Static JSON Schema defaults MAY be used by editor and create interfaces.
Validation-time mutation occurs when the create operation explicitly copies a
default into the draft.

## [#](#update)Update

Update modifies an existing record.

Pipeline:

1. read existing raw frontmatter
2. apply the requested patch
3. re-match and freeze types when type-affecting fields or path changed
4. apply lifecycle `on_update`
5. verify type membership did not change as a lifecycle side effect
6. validate JSON Schema
7. run collection validators
8. write frontmatter and preserve body
9. update derived indexes
10. emit watch/runtime events

If a patch sets a field to missing, the key is removed. If a patch sets a field
to null, the key is persisted as null unless the operation policy says null
means remove.

As an alternative to a frontmatter patch and body replacement, Update accepts `document` containing the complete candidate Markdown source. A document
replacement MUST NOT be combined with `patch`, `fields`, `frontmatter`, or `body`. The candidate is parsed and passes through the same type matching,
lifecycle, validation, concurrency, and atomic-write pipeline as a structured
update. When lifecycle policy does not alter the candidate, the exact supplied
source MUST be preserved. If policy changes persisted values, the authoritative
post-policy source MAY be reserialized and is returned when source was
requested.

Update accepts optional `include_document`. A document replacement implies `include_document: true` for its successful result.

## [#](#delete)Delete

Delete removes a record.

Tools supporting link/reference profiles SHOULD optionally report broken
backlinks before deleting.

Delete is a Core Write operation and MAY emit an event for workflow runtimes.

## [#](#rename)Rename

Rename moves a record within the collection.

Tools MUST reject target paths that escape the collection root.

If reference updating is enabled, link updates SHOULD preserve link style,
alias, and anchor where possible. ID-based links SHOULD not be rewritten if the
target ID did not change.

Atomic reference updates across all affected files belong to a transaction
profile.

## [#](#batch)Batch

Batch operations group operations for validation and reporting.

Recommended behavior:

- validate every operation before writing unless `allow_partial` is true
- support dry-run with full diagnostics
- report per-operation result and diagnostics
- stop on first error unless configured otherwise

## [#](#saved-views)Saved Views

`list_views` discovers the saved-view sources available through the collection
provider. Its result has this shape:

    valid: true
    result:
      views:
        - id: task.views
          name: Task views
          source:
            path: views/tasks.md
            format: mdbase.view
            revision: opaque-source-revision
            writable: true
          views:
            - id: today
              name: Today
              properties:
                - key: title
                  label: Task
                - key: urgency
                  label: Urgency
              presentation:
                type: tasknotes.task-list
      meta:
        total_count: 1
    diagnostics: []

`source.path` is a collection-relative path. `source.format` is a stable source
format identifier. `source.revision` is an opaque token for the source content. `source.writable` describes whether the provider accepts writes for that source
format. Each nested descriptor exposes the stable named-view ID, its display
name, and its optional presentation metadata. Discovery order is ascending by
source path and then source-defined named-view order.

`properties` lists the named view's result values in display order. Each entry
has the result `key` and may include `label`, `description`, `format`, and `hidden` metadata. Canonical view records derive this list from `select`.
Compatible external sources derive it from their ordered property list and
property metadata. Projection and formula results use the same descriptors as
persisted fields.

Malformed configured sources are omitted from `result.views` and reported as
warning diagnostics. Reading a malformed source explicitly produces `invalid_view`.

`execute_view` accepts:

    path: views/tasks.md
    view: today
    context:
      path: projects/alpha.md
    limit: 50
    offset: 0
    render: false

`path` and `view` select a descriptor returned by `list_views`. `context` binds
the invocation context defined in Chapter 11. `limit` and `offset` override the
named view's pagination for this invocation. The provider evaluates the
source's declared expression dialect and returns the query result envelope with `meta.view`.

`render: false` requests the headless result and is the default. `render: true` requests renderer output using the selected presentation metadata.

### [#](#saved-view-source-operations)Saved-view source operations

A provider that advertises a source as `writable: true` MUST support the four
saved-view source operations. These operations exchange the complete source
document so format-aware editors can preserve source data they do not
interpret.

`read_view_source` accepts a `path` from `list_views` and returns:

    path: TaskNotes/Views/tasks.base
    format: obsidian.base
    revision: sha256:opaque
    document: |
      views:
        - type: tasknotesTaskList
          name: Tasks

`create_view_source` accepts `document` and may accept `path`, `format`, and `name`. When `path` is absent, the provider selects a collection-relative path
using the requested format and the collection's format configuration. Creation
MUST validate the complete document and MUST fail with `path_conflict` rather
than replace an existing source.

`update_view_source` accepts `path`, `document`, and optional `if_revision`.
The complete candidate document MUST be valid before the current source is
atomically replaced. `delete_view_source` accepts `path` and optional `if_revision`.

All source operations apply the collection's path-boundary and symlink rules.
Update and delete use the concurrency behavior defined below. A successful
create or update returns the same fields as `read_view_source`; a successful
delete returns `path` and `deleted: true`.

## [#](#concurrency)Concurrency

Write-capable tools SHOULD detect external modification between read and write
using mtime, content hash, version token, or platform-specific file identity.

On conflict, tools MUST preserve the current file and report a concurrency
diagnostic.

Successful reads MUST return a stable `revision` token derived from the raw
file state. Write operations MUST accept an optional `if_revision` token and
fail with `concurrent_modification` when it no longer matches. The token format
is implementation-defined and opaque to callers.

## [#](#operation-result-envelope)Operation Result Envelope

Every operation returns a mapping with:

    valid: true
    result: {}
    diagnostics: []

`valid` is false when any error-severity diagnostic applies. Successful `create`, `update`, and `rename` operations MUST return the complete,
authoritative post-write record document defined in Chapter 03. `rename` adds
its rename-specific metadata to that document.

The document MUST be derived from the bytes actually persisted after lifecycle
hooks, normalization, validation, and the atomic write complete. Clients and
transport adapters MUST NOT reconstruct missing document members from the
request or from another response member.

Create and rename accept optional `include_document`; when true, their
successful record document includes the exact post-write source. Update follows
the source rules above.

Dry runs return operation-specific preflight results rather than record
documents. They use the same envelope and MUST NOT change files, indexes,
runtime state, or revisions.

## [#](#events)Events

After a successful mutation, tools MAY emit watch/runtime events. Events MUST be
delivered after the derived read/query state is consistent. Watch consumers and
workflow runtimes may subscribe to the same stream.

# [#](#13-durable-runtime-companion-profile)13. Durable Runtime Companion Profile

## [#](#purpose)Purpose

The durable runtime profile turns passive, portable definitions into
recoverable automation. It is deliberately a **companion** to two simpler
specifications:

1. the core mdbase contract system defines contract identity, semantic
versions, JSON Schemas, digests, record implementations, resolution, and
packs;
2. the [event/action interoperability profile](#section-05b) defines
CloudEvents, action requests and outcomes, event-source and action-provider
declarations, provider selection, and baseline exchange behavior;
3. this profile adds durable admission, authorization, execution state,
retries, timers, cancellation, and recovery.

The separation is practical. An application can consume portable records or
exchange events and actions without implementing a workflow engine.

This chapter defines runtime companion profile `0.2`. The profile is versioned
independently from mdbase collection specification `0.3.0`.

## [#](#the-short-version)The Short Version

    contract artifact        what an event, action, or record means
    type implements          how a Markdown record stores a record contract
    source/provider declares how live code implements an event/action contract
    runtime policy           whether that exact code may act here
    admitted plan            exact contract digests and implementations for one run
    durable runtime          safely executes and recovers that admitted plan

Installing a contract, type, or pack never runs code and grants no authority.
Markdown provider-registration records are audit evidence only. A host must
verify and admit a live interoperability declaration before code is eligible.

## [#](#no-second-contract-system)No Second Contract System

Runtime profile 0.2 has no runtime-specific contract record, registry,
provider-owned contract definition, event envelope, or contract mode.

- Event and action interfaces are ordinary `mdbase.contract` artifacts.
- Runtime state is stored in ordinary record types that implement standard
record contracts.
- Live event sources and action providers use interoperability declarations.
- Runtime indexes are derived caches of those verified registries.
- Bundled artifacts, installed artifacts, and exported copies have identical
identity and conflict semantics.

Private `x-*` metadata, filenames, and folders have no discovery,
authorization, or activation meaning.

The superseded `type: action`, `type: event`, `type: provider`, `contract_version`, `schemas.payload`, `runtime.contract_mode`, and
provider-supplied contract arrays are not runtime profile 0.2.

## [#](#standard-runtime-pack)Standard Runtime Pack

The official `mdbase.runtime.standard` pack is at [`standard-packs/mdbase-runtime/0.2.0`](https://github.com/mdbase-dev/mdbase-spec/tree/main/standard-packs/mdbase-runtime/0.2.0).
It installs atomically and contains:

| Record contract | Canonical record type | Purpose |
| --- | --- | --- |
| `mdbase.runtime.workflow` | `runtime_workflow` | portable workflow definition |
| `mdbase.runtime.policy` | `runtime_policy` | local authority and provider selection |
| `mdbase.runtime.provider-registration` | `runtime_provider_registration` | optional declaration audit snapshot |
| `mdbase.runtime.capability-grant` | `runtime_capability_grant` | optional materialized grant |
| `mdbase.runtime.run` | `runtime_run` | durable run and admitted plan |
| `mdbase.runtime.action-attempt` | `runtime_action_attempt` | invocation intent and outcome evidence |
| `mdbase.runtime.checkpoint` | `runtime_checkpoint` | durable continuation |
| `mdbase.runtime.timer` | `runtime_timer` | generation-safe one-shot timer |
| `mdbase.runtime.diagnostic` | `runtime_diagnostic` | machine-readable runtime issue |
| `mdbase.runtime.dead-letter` | `runtime_dead_letter` | retained unprocessable evidence |

It also contains inspectable event/action artifacts for the four canonical
record-change events (`mdbase.record.created`, `mdbase.record.modified`, `mdbase.record.deleted`, and `mdbase.record.renamed`), `mdbase.runtime.timer.fired`, and `mdbase.runtime.run.cancel`. The event data
schemas make a contract definition portable across every source and consuming
application; a source declaration says only who publishes an exact artifact.

These artifacts are a standard library, not privileged built-ins. A host may
bundle the pack for offline use without copying it into each collection.
Exporting a bundled artifact is an inspection feature; it does not create a new
definition or alter precedence.

## [#](#runtime-records-are-ordinary-implementations)Runtime Records Are Ordinary Implementations

The canonical workflow type illustrates the model:

    kind: mdbase.type
    name: runtime_workflow
    version: 1
    match:
      where:
        type: runtime_workflow
    schema:
      dialect: json-schema-2020-12
      ref: ../schemas/mdbase.runtime.workflow/1.0.0.schema.json
    implements:
      - contract: mdbase.runtime.workflow
        version: 1.0.0
        fields:
          type: type
          id: id
          version: version
          name: name
          enabled: enabled
          triggers: triggers
          steps: steps

The core collection engine discovers and validates this exactly like any other
record contract. A durable runtime asks the core engine for the verified
descriptor and projected view. It does not scan for `workflows/`, special-case
the filename, or reconstruct a separate registry.

Internal database rows and indexes do not need Markdown record contracts.
Materialize runtime state when it is intended to be portable, inspectable,
exportable, or shared through mdbase. Private storage tables remain
implementation details but must preserve the same protocol evidence.

## [#](#event-sources-and-action-providers)Event Sources And Action Providers

Live implementations use the interoperability profile unchanged.

An event source declaration binds a verified implementation identity to one or
more exact event artifacts. An action provider declaration binds handler IDs to
exact action artifacts. Declarations include their own canonical digest.

A provider does not return an action or event definition from executable code.
Registration supplies the already-resolved artifact to the interoperability
bridge, which validates the implementation binding and publishes a declaration.

Several sources may publish one event contract. Several providers may implement
one action contract. Events remain multicast. Actions select exactly one
provider:

- an explicit provider selector in the workflow wins;
- otherwise a single matching runtime-policy selection may apply;
- otherwise one eligible provider is unambiguous;
- zero providers fails with `no_provider`;
- multiple providers fail with `ambiguous_provider`.

Executable providers are never unioned.

## [#](#authorization-is-separate)Authorization Is Separate

Conformance answers “can this implementation speak this interface?”
Authorization answers “may it act here, for this caller, on this resource?”

A host checks, independently:

- selected and enabled runtime policy;
- caller and executor identity;
- named capabilities;
- collection and record scope;
- action contract and exact provider implementation;
- operational limits;
- required user approval;
- transport and host authority.

Deny takes precedence. The standard pack ships no permissive policy. A
contract, type, workflow, provider declaration, or audit record is passive
until a host admits it under current authority.

## [#](#workflow-requirements)Workflow Requirements

Authors use a contract ID and SemVer requirement:

    type: runtime_workflow
    id: canvas.zone.set-status
    version: 1.0.0
    name: Set status from canvas zone
    enabled: true

    triggers:
      - id: drop
        event:
          id: canvas.drop
          version: ^1.0.0

    steps:
      - id: patch-status
        action:
          id: mdbase.record.patch
          version: ^2.0.0
        requires:
          capabilities: [mdbase.record.write]
        input:
          path:
            $expr: event.data.file.path
          patch:
            status:
              $expr: event.data.zone.id

The readable workflow does not contain a digest for every normal dependency.
An author may add a digest to a requirement when deliberately pinning it.

Workflows normally depend on behavior, not an application name. Add a provider
selector only when the workflow intentionally needs a provider-specific
implementation:

    provider:
      application: tasknotes
      implementation: tasknotes-v5

## [#](#preflight-and-admission)Preflight And Admission

Preflight is derived and repeatable. It:

1. validates the workflow through `mdbase.runtime.workflow`;
2. checks unique trigger and step IDs and compiles expressions;
3. resolves each contract requirement through the core contract registry;
4. uses verified interoperability declarations to find implementations;
5. applies explicit or policy provider selection;
6. evaluates capability and operational policy;
7. reports every missing, conflicting, ambiguous, or denied dependency.

Preflight does not make an execution durable.

Admission occurs for one validated event delivery. In one durable transaction
the authority:

1. journals and deduplicates the CloudEvent;
2. identifies the exact event-source declaration;
3. evaluates trigger and admission policy;
4. resolves every action provider;
5. pins the workflow revision;
6. pins exact event/action contract ID, version, and digest;
7. pins exact source/provider identities and declaration digests;
8. records handler IDs, idempotency decisions, and deadlines;
9. reserves the run idempotency key and concurrency position;
10. writes the run and immutable admitted plan.

Registry changes after this transaction do not alter the plan. Changing an
admitted dependency requires explicit migration or re-admission.

## [#](#shared-envelopes)Shared Envelopes

Runtime event intake is a structured CloudEvents 1.0 envelope from the
interoperability profile. Runtime action dispatch persists and exchanges the
standard:

- `mdbase.action.request`;
- `mdbase.action.invocation`;
- `mdbase.action.outcome`;
- `mdbase.action.cancel`.

The runtime does not wrap these in another envelope or rename `data` to `payload`. Runtime-specific run, attempt, cursor, and lease records refer to
the shared envelope evidence.

## [#](#runtime-configuration)Runtime Configuration

The core `mdbase.yaml` schema does not define `runtime.contract_mode`.
Runtime enablement, storage, worker identity, policy selection, and transport
binding are host configuration. A host may store portable policy records in the
collection, but loading the collection remains safe and does not activate them.

## [#](#conformance-boundary)Conformance Boundary

A conformant core engine supports contract artifacts, type implementations,
record projection, conflict detection, and packs. It need not execute
workflows.

A conformant interoperability implementation supports its claimed event/action
roles. It need not persist runs.

A conformant durable runtime supports admission and the execution/recovery
rules in this chapter and Chapter 14. It uses core and interoperability
evidence rather than publishing a second contract model.

# [#](#14-durable-workflow-execution)14. Durable Workflow Execution

## [#](#purpose)Purpose

Chapter 13 defines the companion-profile boundary, standard records, policy,
and admission. This chapter defines the durable protocol after a workflow and
event have been admitted.

JSON Schema proves that persisted evidence has the right shape. The transition,
transaction, lease, replay, and recovery rules below are behavioral
requirements and cannot be replaced by schema validation.

## [#](#execution-inputs)Execution Inputs

A worker executes only an immutable admitted plan. The plan contains:

- workflow content revision;
- exact triggering event contract and source declaration;
- exact action contracts;
- one exact provider identity, declaration digest, and handler ID per step;
- selected runtime policy revision;
- evaluated limits and authority evidence;
- run idempotency and concurrency decisions.

The worker may confirm that a live declaration still matches the pin. It must
not silently resolve a replacement.

## [#](#deterministic-evaluation)Deterministic Evaluation

Workflow variables, conditions, templates, and iteration use the expression
rules in Chapter 11. Runtime activation exposes the shared CloudEvent as `event`; portable payload values therefore appear below `event.data`.

- variables are evaluated once in dependency order;
- cycles fail preflight;
- trigger and step conditions must evaluate to boolean true to continue;
- object values are literals unless they have exactly one `$expr` key;
- steps execute in document order;
- `for_each` visits list items in list order;
- evaluated action input is validated by the interoperability bridge before
provider invocation;
- action output or declared error data is validated before it becomes durable
step state.

Every execution limit is the stricter of the workflow and current policy.

## [#](#durable-event-admission)Durable Event Admission

Event delivery may be repeated. The data authority therefore commits the
following atomically:

- validated CloudEvent and exact source evidence;
- duplicate/tombstone state;
- monotonically increasing delivery cursor;
- every run admitted from the event;
- run idempotency reservations;
- concurrency queue changes;
- immutable admitted plans.

A crash cannot expose the event without its derived runs or a run without its
event. Retrying the same event ID and contract/source evidence returns the
original cursor and does not create another logical run. Reuse of an event ID
with different canonical content fails closed.

Pruning may remove event bodies after the documented retention period but
keeps sufficient tombstone evidence through the replay horizon. Reading from a
cursor older than retained history reports `cursor_expired` and the earliest
available cursor; it never silently skips the gap.

## [#](#run-state-machine)Run State Machine

Portable run states and transitions are:

| From | To | Cause |
| --- | --- | --- |
| admitted | `queued` | concurrency delays execution |
| admitted or `queued` | `running` | worker obtains current lease |
| `running` | `waiting` | durable checkpoint suspends execution |
| `waiting` | `queued` | checkpoint becomes ready |
| `running` | `succeeded` | all required steps complete |
| `running` | `failed` | deterministic failure or deadline |
| `queued`, `running`, `waiting` | `cancelled` | cancellation completes without ambiguous effects |
| `running` | `indeterminate` | non-replayable action outcome is unknown |

`succeeded`, `failed`, `cancelled`, and `indeterminate` are terminal. A host
rejects every transition from a terminal state.

## [#](#leases)Leases

A worker claims a run with a bounded lease containing owner, opaque token, and
expiry. Every state-changing write compares the current lease token.

- workers renew before expiry;
- an expired lease may be recovered by another worker;
- a stale worker cannot commit a step, checkpoint, or terminal state;
- handler execution is never treated as proof that its outcome was committed.

Lease duration and renewal strategy are host configuration. The observable
stale-write rejection is portable.

## [#](#action-attempts)Action Attempts

Before invoking a provider, the runtime durably writes an action-attempt intent
containing the shared request ID, invocation ID, attempt ID, exact contract,
provider identity, provider-declaration digest, handler ID, idempotency key,
deadline, and input.

It then sends the ordinary interoperability request. The bridge produces the
ordinary invocation and outcome evidence. The runtime stores those envelopes
without redefining them.

Attempt states are:

    admitted → dispatching → succeeded
                          ↘ rejected
                          ↘ failed
                          ↘ cancelled
                          ↘ indeterminate

Only an outcome with matching request, invocation, attempt, contract, and
provider evidence completes the attempt.

## [#](#idempotency-and-crash-recovery)Idempotency And Crash Recovery

The workflow run has an idempotency key. If the author does not supply one, the
host derives:

    workflow.id + ":" + event.id + ":" + trigger.id

The key is reserved during admission, before any dispatch.

Action replay follows the action artifact and provider declaration:

- for request-idempotent actions, recovery reuses the same request and
invocation identity as required by the binding and obtains the retained
outcome or safely retries;
- for non-idempotent actions, a crash after dispatch but before durable outcome
creates `indeterminate`; the runtime does not guess or replay;
- conflicting reuse of a request or idempotency key fails closed;
- an action marked as requiring idempotency is ineligible when the provider or
transport cannot supply it.

Exactly-once external side effects are not promised. The profile provides
durable intent, duplicate suppression, explicit replay rules, and honest
indeterminate outcomes.

## [#](#concurrency)Concurrency

The optional workflow concurrency policy applies to an evaluated group:

| Policy | New run while group is active |
| --- | --- |
| `skip` | record the admission decision and do not execute |
| `queue` | preserve event-cursor order |
| `replace` | request cancellation, then wait for a terminal state |
| `allow` | run concurrently |

When no group expression is supplied, the workflow ID is the group. Completed
effects are never rolled back by `replace`. A replacement waits when the active
action cannot be cancelled safely.

## [#](#cancellation-and-deadlines)Cancellation And Deadlines

Cancellation is cooperative and uses the interoperability cancellation
envelope. The runtime records the request before delivery.

- not-yet-started attempts are not dispatched;
- cooperative providers receive cancellation through the bridge;
- a confirmed cancelled outcome permits `cancelled`;
- a completed action outcome wins a race with cancellation;
- loss of a non-idempotent outcome becomes `indeterminate`;
- terminal runs ignore repeated cancellation requests idempotently.

A run deadline prevents all new dispatches after expiry. A deterministic
deadline failure becomes `failed`; an already-dispatched action whose outcome
cannot be known follows the indeterminate rule.

## [#](#checkpoints)Checkpoints

A checkpoint stores enough state to resume deterministically:

- run ID and current generation;
- next step/item position;
- immutable variables and completed step outputs;
- pending request/invocation evidence;
- wait condition;
- current workflow/admitted-plan revision;
- lease-safe updated time.

Checkpoint generation only increases. A stale generation or stale lease write
is rejected. Completion is recorded before the worker advances.

## [#](#timers)Timers

A durable timer is generation-safe and one-shot.

1. scheduling writes generation `n`, fire time, and an event contract
requirement plus event data;
2. rescheduling writes generation `n + 1` and makes older generations stale;
3. a worker atomically claims the current due generation;
4. firing publishes `mdbase.runtime.timer.fired` or the requested event through
an interoperability event source;
5. the resulting CloudEvent is admitted through the ordinary event journal;
6. the timer becomes `fired` only with matching event evidence.

After restart, an overdue current generation fires once under `missed_run_policy: fire_once`. Polling or racing workers cannot create two
logical events.

## [#](#policy-rechecks)Policy Rechecks

Admission pins compatibility and selection. Authority is checked again
immediately before each dispatch because grants, scopes, or approval may have
changed.

A new denial prevents dispatch and fails the attempt with `capability_denied`. A policy change does not substitute another provider or
contract in an admitted plan. That requires re-admission.

## [#](#diagnostics-and-dead-letters)Diagnostics And Dead Letters

Protocol failures use stable codes and durable evidence. Important examples
include:

- `unknown_contract`;
- `contract_digest_conflict`;
- `event_source_unavailable`;
- `no_provider`;
- `ambiguous_provider`;
- `capability_denied`;
- `idempotency_unavailable`;
- `invalid_run_transition`;
- `stale_lease`;
- `cursor_expired`;
- `outcome_indeterminate`.

Unprocessable event/request/outcome evidence may be retained as `mdbase.runtime.dead-letter`. A dead-letter record is passive. Requeue is an
explicit authorized operation that creates new admission evidence.

## [#](#minimum-durable-conformance)Minimum Durable Conformance

A runtime claiming profile 0.2 must demonstrate:

- ordinary record-contract validation of runtime records;
- exact contract/source/provider pinning through interoperability declarations;
- authorization separate from conformance;
- atomic event/run admission and duplicate suppression;
- legal run and attempt transitions;
- stale-lease rejection and crash recovery;
- idempotent versus non-idempotent recovery;
- generation-safe timers;
- cursor expiry behavior;
- cancellation and deadline races;
- machine-readable diagnostics.

The reference TypeScript executor demonstrates the boundary and shared
exchange model but is intentionally non-durable. Durable conformance belongs to
hosts such as the Rust runtime and Connect binding.

# [#](#15-migrations-and-compatibility)15. Migrations And Compatibility

## [#](#migration-philosophy)Migration Philosophy

v0.3 uses the source model defined by Chapters 01–14. Migration translates
v0.2.x collections into that model and reports features that need adapter-owned
handling.

Migration tooling should produce:

- a readable diff
- a machine-readable report
- explicit unsupported-feature notes
- generated output only with user approval or generated-file detection

Migration analyzes the complete collection. Before a write, tooling MUST
validate every existing record against the proposed target types and include
incompatible records in the report.

## [#](#type-mapping)Type Mapping

| v0.2.x feature | v0.3 destination |
| --- | --- |
| `fields` | JSON Schema `properties` |
| field `required` | JSON Schema root `required` |
| `type: string` | `{ type: "string" }` |
| `type: integer` | `{ type: "integer" }` |
| `type: number` | `{ type: "number" }` |
| `type: boolean` | `{ type: "boolean" }` |
| `type: date` | `{ type: "string", format: "date" }` |
| `type: datetime` | `{ type: "string", format: "date-time" }` |
| `type: time` | `{ type: "string", format: "time" }` |
| `type: enum`, `values` | JSON Schema `enum` |
| `type: list` | JSON Schema `array` |
| `type: object` | JSON Schema `object` |
| `type: link` | JSON Schema string/array plus `collection.links` |
| `default` | JSON Schema `default` and/or `collection.read_defaults` |
| `strict` | `additionalProperties` |
| `unique` | `collection.unique` |
| `generated` | `lifecycle` |
| `computed` | query projection, collection projection, or workflow |
| `path_pattern` | `collection.path.pattern` |
| `display_name_key` | `collection.display.name_field` |
| `extends` | JSON Schema `$ref`/`allOf` or explicit duplication |

## [#](#defaults)Defaults

Migration should distinguish:

- creation/editor hints: JSON Schema `default`
- effective read/query defaults: `collection.read_defaults`
- dynamic values: `lifecycle`

For current mdbase field defaults, the safest migration is to emit both JSON
Schema `default` and `collection.read_defaults` for static scalar defaults, with
a report explaining the difference.

## [#](#generated-fields)Generated Fields

Generated fields migrate to lifecycle:

| v0.2.x generated | v0.3 lifecycle |
| --- | --- |
| `now` | `on_create.set.field: { now: true }` |
| `now_on_write` | `on_update.set.field: { now: true }` |
| `uuid` | `{ uuid: true }` |
| `ulid` | `{ ulid: true }` |
| `slugify from field` | `{ slugify: field }` |

## [#](#computed-fields)Computed Fields

Computed fields migrate to one of:

- query projection suggestion
- `collection.projections` when the target tool supports it
- workflow/runtime policy when the computed value should be materialized
- unsupported note when the computation has side effects or depends on
non-portable functions

## [#](#expressions)Expressions

Current mdbase expressions migrate to CEL where possible.

Tool-specific expression dialects are adapter concerns. Tools may translate
them to CEL for portable storage and translate them back for user interfaces or
exports.

## [#](#obsidian-bases-views)Obsidian Bases Views

Obsidian `.base` files are external saved-view sources. A collection provider
discovers configured sources and exposes them through the saved-view operations
in Chapter 12. Core Read continues to discover records through the collection's
record extensions.

Collections enable discovery with a namespaced configuration section:

    x-obsidian:
      bases:
        include:
          - TaskNotes/Views/**/*.base
        create_folder: TaskNotes/Views
        default_for_new_views: true

`include` contains collection-relative glob patterns. The provider MUST apply
the same path-boundary and symlink protections used for record discovery. `create_folder` identifies the preferred location for new Obsidian sources. `default_for_new_views` makes that source format the collection's default when
a view-creation interface offers no explicit format. Providers advertising
write support use these values when creating a source.

Write-capable providers validate the complete `.base` document before a
source operation commits it. They preserve unknown top-level keys, view keys,
property metadata, formulas, and presentation options supplied in the
document. A source editor can therefore modify the structures it understands
while round-tripping the remainder.

The `.base` file remains authoritative for a discovered Obsidian source. `list_views` returns `source.format: obsidian.base`, a revision derived from the
source bytes, and a stable named-view ID for each contained view. Stable IDs are
derived deterministically from view names when the source format supplies no
ID. Collisions receive deterministic source-order suffixes.

The structural mapping is:

| Obsidian Bases | mdbase view record |
| --- | --- |
| global `filters` | `query.where` |
| view `filters` | named-view `where`, combined with the shared filter |
| `formulas` | `query.projections` |
| `formula.name` | `projection.name` |
| `properties` | property metadata |
| view `order` | `select` order |
| view `sort` | `order_by` |
| `groupBy` | `group_by` |
| custom and property summaries | `summary_functions` and `summaries` |
| view `type` | `presentation.type` |
| plugin view keys | presentation options or `x-*` extension data |

Executing an Obsidian source evaluates its filters and formulas with Obsidian
Bases expression semantics. The adapter parses the source dialect into an
inspectable syntax tree and applies the source dialect's value coercion, date,
link, file, formula, and error behavior. Translation to canonical CEL is an
export operation and succeeds only when behavior is preserved. Translation
diagnostics identify unsupported expressions, functions, coercions, or
renderer features. Lossless source and round-trip metadata may be retained
under `x-obsidian`.

Execution returns the headless result envelope from Chapter 12. Selected and
presentation-mapped values appear under each row's `values`; renderer metadata
may approximate layout while retaining the source's filtering, formula,
ordering, grouping, and pagination semantics.

Obsidian placement state maps to the portable invocation context rather than to
query semantics: opening a Base directly supplies the view definition, an
embed supplies its embedding record, and an active-file interface supplies its
active record. The adapter resolves that host state into an explicit invocation
context before calling the query or view executor.

## [#](#runtime-workflows)Runtime Workflows

Current generated-field and tool-conforming behavior that causes mutation
should be reviewed as lifecycle or workflow behavior.

Generated IDs and timestamps usually become lifecycle.

Cross-record behavior, agent work, approval flows, external APIs, and scheduled
checks become workflows and action/event contracts.

## [#](#data-contract-implementations)Data Contract Implementations

Portable application interfaces migrate to a local data contract plus a
type-local `implements` entry.

Example:

    implements:
      - contract: example.task
        version: 1.0.0
        fields:
          status: status
        binding:
          completed_values: [done, cancelled]

The migration bundle MUST include the exact `mdbase.contract` artifact required
by the implementation. Existing convention-based `x-*` objects do not become
contracts merely because they contain keys named `contract` or `version`.

Private annotations with no portable contract meaning remain under a
namespaced `x-*` section. They SHOULD NOT become JSON Schema custom keywords.

## [#](#version-detection)Version Detection

A v0.2.x type file usually has `name` and `fields` without `kind: mdbase.type`.

A v0.3 type file has `kind: mdbase.type` and `schema`.

Migration tooling SHOULD refuse ambiguous files unless the user supplies an
explicit source version.

## [#](#safe-migration-protocol)Safe Migration Protocol

A conforming migration command has separate analyze and apply phases:

1. discover source config, type files, extension metadata, and records
2. generate proposed config/types without modifying the collection
3. validate generated schemas and type files
4. validate all existing records against the proposed target
5. emit a human-readable diff and machine-readable report
6. require explicit approval unless every target is recognized as generated
7. create a backup manifest containing hashes and original paths
8. write through temporary files and atomic renames where supported
9. re-open and validate the migrated collection

Apply MUST abort before writes when analysis has unsupported features or invalid
target records unless the caller explicitly selects a documented partial mode.
A failed apply restores files from the backup manifest when restoration is
possible and reports any paths requiring manual recovery.

Apply SHOULD durably journal the currently attempted path and completed paths
inside the backup before each replacement. Tooling MUST provide a recovery path
that can resume or restore an interrupted apply. Restoration MUST validate the
backup hashes from the manifest before replacing current files and MUST require
explicit approval when invoked as a separate command.

Repeated analysis of unchanged inputs MUST produce the same report. Re-applying
an already migrated type MUST fail without writing.

Unknown source metadata is preserved under `x-legacy-v0.2` with its original
key paths unless a named migration adapter handles it. YAML comments and style
preservation are best effort. Markdown bodies MUST be preserved byte for byte.

The report includes source and target hashes, generated-file evidence, exact
mappings, warnings, unsupported constructs, invalid record paths, proposed file
operations, backup location, and post-apply validation status.

# [#](#16-conformance)16. Conformance

## [#](#conformance-profiles)Conformance Profiles

A v0.3 conformance claim names the atomic behavior sets an implementation has
verified. Profiles let readers distinguish collection reading, matching,
queries, writes, runtime preflight, workflow execution, and watching.

| Profile | Purpose |
| --- | --- |
| Core Read | discover collections, parse records, load types and data contracts, and validate JSON Schema |
| Collection Semantics | apply defaults, uniqueness, path policy, multi-type composition, and collection diagnostics |
| CEL | compile and evaluate the shared mdbase CEL language and host contract |
| CEL Match | evaluate `match.expr` against raw candidate records |
| Query | evaluate contextual CEL filters, projections, grouping, summaries, and query envelopes |
| Links | parse, resolve, validate, and traverse links |
| Core Write | create, update, delete, rename, and batch records |
| Lifecycle | apply standard managed-field policy during writes |
| Event/Action Interoperability | exchange CloudEvents and admitted action invocations through independently claimable roles |
| Durable Runtime 0.2 | validate standard runtime records, admit exact plans through interoperability declarations, and execute/recover them durably |
| Watch | report ordered collection changes after consistent state |

Normative profile IDs and dependencies are:

| Profile ID | Requires |
| --- | --- |
| `core_read` | none |
| `collection_semantics` | `core_read` |
| `cel` | none |
| `cel_match` | `core_read`, `cel` |
| `cel_query` | `core_read`, `cel` |
| `links` | `collection_semantics`, `cel` |
| `core_write` | `collection_semantics` |
| `lifecycle` | `core_write`, `cel` |
| `event_action_interop/0.1` | none |
| `runtime/0.2` | `core_read`, `event_action_interop/0.1`, `cel` |
| `watch` | `core_read` |

An implementation claims a profile after passing every required behavior and
test for that profile. `optional_features` records additional work outside the
verified profile list.

### [#](#conformance-claim-documents)Conformance Claim Documents

Conformance claims MUST validate against `schemas/v0.3/conformance-claim.schema.json`. A claim names:

- the implementation and exact implementation version
- the exact mdbase specification version
- every supported profile ID, including dependency profiles
- the runtime profile version when a runtime profile is claimed
- the supported JSON Schema keywords and formats
- operational limits that affect portable behavior
- evidence commands and the time and environment in which they passed

A general compatibility label is supported by this validated profile list.
Profile claims derive from verified behavior independently of product roles such
as LSP, CLI, plugin, and server.

`v0_2_read` and `v0_2_migrate` are compatibility declarations for transition
behavior. They appear separately from v0.3 profiles.

The canonical claim schema enforces profile dependencies. Claim verification
tools SHOULD reject evidence produced for a different implementation artifact
or specification version and SHOULD report stale evidence.

### [#](#portable-interoperability-testbed)Portable Interoperability Testbed

The spec-owned interoperability testbed in `testbed/v0.1/` supplements the
profile suites with executable, implementation-neutral integration scenarios.
It has three rings:

| Ring | Boundary under test |
| --- | --- |
| Contract | record contracts, type `implements` declarations, projections, and multiple independent consumers |
| Interop | live event sources/consumers and action callers/providers connected through the interoperability profile |
| Runtime | durable admission, action attempts, crash recovery, leases, and fencing |

The testbed is deliberately role-based. An application MAY consume the same
type or contract as any number of other applications. An event is delivered to
every compatible authorized subscriber. An action still resolves to exactly one
admitted provider; multiple eligible providers remain an error until a caller
or policy selects one. The testbed does not turn an application name into a
contract owner.

Every testbed scenario MUST validate against `schemas/testbed/v0.1/scenario.schema.json`, use only fixtures from the validated
neutral catalog, and include its canonical expected transcript. An adapter MUST
run behind the `describe`/`run` process boundary defined by testbed protocol `0.1`. A runner MUST start a fresh adapter process for each scenario, validate
the run request and returned transcript, and compare the complete ordered
transcript entries. Adapters MUST derive entries from behavior observed through
public or documented implementation boundaries; copying the scenario's
expected entries is not a conforming adapter.

Transcripts intentionally contain stable facts rather than private state.
Generated IDs, database keys, wall-clock values, stack traces, scheduling
jitter, and implementation-specific objects MUST be normalized or omitted
unless a scenario explicitly makes them observable. Scenario order, actor,
operation, outcome, and facts are all significant.

Portable testbed evidence validates against `schemas/testbed/v0.1/evidence.schema.json` and binds every scenario result to a
canonical transcript digest. A conformance claim records it with evidence kind `testbed_transcript`, the protocol version, scenario IDs, artifact path, and
evidence digest. `evidence_digest` MUST be the SHA-256 digest of the evidence
artifact's recursively key-sorted, whitespace-free JSON form, prefixed with `sha256:`. Passing the testbed does not replace the complete profile suite: it
is integration evidence for the normative requirements named by each scenario.

Conformance never grants authority. A testbed adapter may be able to construct
a compatible payload or declaration while the real host correctly rejects the
operation as unauthorized.

## [#](#canonical-diagnostics)Canonical Diagnostics

Every diagnostic contains:

    severity: error
    code: type_conflict
    message: Human-readable context
    path: tasks/example.md
    field: status
    type: task
    schema_location: "https://mdbase.dev/schemas/v0.3/type-file.schema.json#/..."
    details: {}

`severity`, `code`, and `message` are required. Paths use collection-relative
forward-slash form. `field` uses JSON Pointer or an explicitly identified
frontmatter selector. Implementations MAY add fields under `x-*`.

The v0.3 core codes include `unsupported_profile`, `type_conflict`, `type_membership_changed`, `path_value_missing`, `schema_ref_forbidden`, `schema_ref_unresolved`, `schema_ref_cycle`, `format_invalid`, `lifecycle_expression_error`, `concurrent_modification`, `invalid_query`, `context_not_found`, `context_required`, `context_type_mismatch`, `view_not_found`, `invalid_view`, `unsupported_presentation`, `invalid_data_contract`, `data_contract_not_found`, `data_contract_conflict`, `data_contract_version_mismatch`, `data_contract_binding_invalid`, `data_contract_field_invalid`, and `data_contract_record_invalid`. Runtime profile 0.2 reuses interoperability
codes such as `unknown_contract`, `contract_digest_conflict`, `no_provider`, `ambiguous_provider`, and `capability_denied`, and additionally defines `event_source_unavailable`, `idempotency_unavailable`, `cursor_expired`, `stale_lease`, `invalid_run_transition`, `outcome_indeterminate`, and `stale_timer_generation`.

## [#](#core-read-requirements)Core Read Requirements

Core Read implementations MUST:

- identify a collection by `mdbase.yaml`
- scan records using collection-relative forward-slash paths
- load and validate v0.3 type files
- load exact-version data contracts and validate type `implements` entries
- expose deterministic contract and implementation digests
- project and validate contract views when a record is accessed through an
implementation
- validate embedded JSON Schema against the v0.3 profile
- select explicit types and evaluate structured inferred match rules
- validate raw frontmatter independently against every matched schema
- reject a type that requires an unsupported optional profile with
`unsupported_profile`
- report diagnostics in the canonical machine-readable shape

## [#](#collection-semantics-requirements)Collection Semantics Requirements

Collection Semantics implementations MUST:

- apply `collection.read_defaults` to effective reads
- preserve missing, null, raw, and effective distinctions
- validate every `collection.unique` rule in its declared scope
- validate portable `collection.path` policies for write-capable tools
- expose display metadata as advisory values
- compose compatible behavior from multiple matched types
- report `type_conflict` for incompatible matched behavior

## [#](#cel-requirements)CEL Requirements

CEL implementations MUST:

- compile and evaluate portable CEL source used by a claimed feature context
- supply the system bindings defined for that context in Chapter 10
- preserve the mdbase missing, null, raw, and effective-value contract
- provide `now()`, `today()`, and `duration()` with declared timezone behavior
- enforce and report expression, evaluation, and traversal limits
- distinguish compilation diagnostics from evaluation diagnostics

The CEL profile supplies the shared language capability. Each embedding profile
defines its context and operational outcome.

## [#](#cel-match-requirements)CEL Match Requirements

CEL Match implementations MUST:

- compile `match.expr` while loading its type definition
- evaluate it against raw candidate frontmatter and file metadata
- combine it with other members of `match` using AND
- match only a boolean true result
- report per-record evaluation errors and treat that candidate as a non-match

## [#](#query-requirements)Query Requirements

Query implementations MUST:

- validate portable query objects against the canonical query schema
- resolve and snapshot an optional same-collection invocation context
- expose the complete `this` context contract, binding it to null when absent
- evaluate named query projections in dependency order before filtering
- reject cyclic projection dependencies and duplicate result names with
`invalid_query`
- evaluate `where` filters against the effective query context
- evaluate requested CEL projections
- support OR-based type filtering
- support deterministic ordering and pagination
- support deterministic grouping and built-in and custom summaries
- return total-count and has-more metadata
- return context, grouping, and summary metadata when requested
- expose raw and effective frontmatter when requested
- report per-record evaluation errors and continue evaluating remaining records

## [#](#view-record-optional-feature)View Record Optional Feature

An implementation advertises `view_records` through `optional_features` when it:

- validates view frontmatter against the canonical view schema
- lists view records with stable source and named-view descriptors
- resolves a stable view-record ID or path plus a stable named-view ID
- rejects duplicate named-view IDs with `invalid_view`
- derives the executable query using the inheritance and merge rules in
Chapter 11
- exposes selected result properties in display order with their metadata
- applies `context.this.on_missing` and context type constraints before query
execution
- reports the selected view and resolved context in query result metadata
- applies presentation metadata as advisory input while preserving canonical
headless results
- keeps alternate dialect and renderer-specific data under `x-*` extensions

A tool MAY advertise supported presentation identifiers separately in `optional_features`.

An implementation advertises `obsidian_bases_views` through `optional_features` when it:

- discovers `.base` sources selected by `x-obsidian.bases.include`
- assigns deterministic named-view IDs and source revisions
- parses filters and formulas before candidate evaluation
- evaluates the Obsidian Bases expression dialect, including formula
dependencies, file and link values, date and duration behavior, methods, and
coercions
- combines source and named-view filters with AND
- applies source order, sort, group, limit, and presentation metadata
- exposes the source's ordered properties and display names
- returns the saved-view headless result envelope
- keeps `.base` sources authoritative throughout discovery and execution

An implementation advertises `writable_view_sources` through `optional_features` when it:

- marks only writable source formats with `source.writable: true`
- reads complete source documents with stable opaque revisions
- validates complete candidate documents before create or update
- creates sources without replacing an existing path
- applies `if_revision` to update and delete
- writes source replacements atomically
- preserves source-format extension data supplied by the caller
- makes successful mutations visible to subsequent list and execute operations

Conformance suites for `obsidian_bases_views` MUST include an oracle corpus
captured from the supported Obsidian expression environment. Each case records
the expression, evaluation context, expected value or error, and source
environment version. Known upstream divergences are identified individually in
the corpus.

## [#](#links-requirements)Links Requirements

Links implementations MUST:

- parse wikilinks, Markdown links, and bare path link values
- resolve collection-relative and file-relative paths safely
- enforce `collection.links.target_type` and `validate_exists`
- expose `file.links`, `file.embeds`, and `file.tags`
- provide the CEL link helpers from Chapter 10
- bound `asFile()` traversal

## [#](#core-write-requirements)Core Write Requirements

Core Write implementations MUST:

- validate a complete draft before writing
- preserve unrelated Markdown body content where possible
- reject paths that escape the collection root
- enforce `if_revision` and report common concurrency conflicts
- return the canonical operation envelope and final record revision
- update derived state before reporting a successful mutation

## [#](#lifecycle-requirements)Lifecycle Requirements

Lifecycle implementations MUST:

- support `on_create` and `on_update`
- support `now`, `today`, `uuid`, `ulid`, `slugify`, `copy`, and `literal` value providers
- evaluate lifecycle guards in the lifecycle CEL context
- run lifecycle before final record validation
- evaluate membership once after lifecycle and report
`type_membership_changed`
- report conflicts between matched lifecycle policies

## [#](#runtime-contracts-requirements)Runtime Contracts Requirements

Runtime Contracts implementations MUST:

- load provider, action, event, capability, policy, and workflow contract records
- represent implicit contracts supplied by a runtime
- compose the effective registry deterministically
- validate strict contract shapes and `x-*` extension names
- validate embedded action and event JSON Schemas
- validate event envelopes, payloads, action inputs, and action outputs
- resolve workflow event, action, capability, and provider references
- reject duplicate trigger and step IDs within a workflow
- resolve provider listings and runtime-policy workflow and capability selectors
- fail preflight for denied required capabilities
- fail preflight with `executor_not_selected` when policy selects no executor
- include capabilities supplied by providers, actions, and policy in the
effective capability set
- materialize implicit contracts when claiming materialization support

This profile covers registry composition, contract validation, authorization
preflight, and materialization. Workflow execution has its own profile.

## [#](#workflow-requirements)Workflow Requirements

Workflow implementations MUST:

- follow the preflight and event-to-run sequence in Chapter 14
- atomically journal, deduplicate, and admit delivered events
- apply trigger debounce and minimum-interval admission
- evaluate workflow variables and detect dependency cycles
- evaluate trigger, workflow, and step conditions in their defined contexts
- evaluate step inputs and iteration in deterministic order
- validate evaluated inputs before dispatch
- perform dispatch-time capability authorization
- apply execution mode and runtime-policy executor selection
- reserve idempotency keys with the required executor scope
- apply concurrency policy, run limits, and `on_error`
- pin canonical workflow, registry, action, and policy revisions
- claim work with bounded leases and reject stale lease writes
- persist stable invocation IDs and dispatch intents before provider calls
- recover idempotent attempts and mark ambiguous non-idempotent attempts
`indeterminate`
- record standard run, step, attempt, receipt, and checkpoint results
- validate action outputs and emitted events
- commit action results and emitted-event admissions atomically
- provide generation-safe one-shot timer upsert, cancel, fire, and missed-run
behavior when claiming canonical timer support
- report unsupported actions, capabilities, and unsafe execution state through
runtime diagnostics

Action handlers are runtime-specific contracts in the effective registry. A
claim lists its tested handler set under `optional_features` or associated
evidence.

## [#](#watch-requirements)Watch Requirements

A watch notification MUST identify a change kind, unique event ID, observation
time, and affected collection-relative path or contract identity. A record
notification has this portable shape:

    kind: record_modified
    id: watch_01J...
    observed_at: "2026-07-19T10:30:00Z"
    path: tasks/example.md
    changed_fields: [status]
    frontmatter:
      type: task
      title: Example
      status: done

Record notifications use these change kinds:

- `record_created`
- `record_modified`
- `record_deleted`
- `record_renamed`

Config, type, and runtime-aware implementations also use `config_changed`, `type_changed`, and `runtime_registry_changed` as applicable. A rename may be
reported as `record_deleted` followed by `record_created` when the host cannot
establish file identity.

`record_created` and `record_modified` include current effective frontmatter. `record_renamed` includes `path`, `previous_path`, and current effective
frontmatter. `record_modified.changed_fields` contains the top-level raw
frontmatter keys whose persisted values changed; it is empty for a body-only
change. A deleted notification may include the last observed frontmatter.

Watch implementations MUST:

- observe the same record extensions, exclusions, type folder, and nested
collection boundaries as Core Read
- report logical changes caused by external file updates and core write
operations
- publish record, config, and type notifications after derived read and query
state reflects the change
- include both paths in a detected rename
- preserve notification order for the same record or configuration subject
- coalesce duplicate host notifications for one logical change and report the
final observed state
- isolate listener failures so later notifications continue to be delivered

An implementation that also claims Runtime Contracts reports effective registry
changes after registry recomposition through `runtime_registry_changed`.