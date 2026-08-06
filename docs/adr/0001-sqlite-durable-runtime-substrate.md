---
status: accepted
---

# SQLite as the durable-runtime persistence substrate

Durable runtime state (Ch. 13/14: runs, admitted plans, checkpoints, leases, timers, action attempts, dead letters) is not stored as ordinary Markdown records mutated through the Core Write pipeline (#12). #12's write model gives file-scoped optimistic concurrency only — one atomic file replace per write, keyed on a SHA-256 content hash — but Ch.14 requires atomic *multi*-record admission (validated event + duplicate/tombstone state + monotonic delivery cursor + admitted runs + idempotency reservations + concurrency-queue changes + immutable admitted plans, committed together in one durable transaction), CAS-token leases on every state-changing write, monotonically-increasing checkpoint generations, and generation-safe timer claiming under concurrent workers. None of that is achievable with independent per-file CAS without hand-rolling a second transaction log on top of the filesystem — which would just be a worse SQLite.

We decided: SQLite (via `Microsoft.Data.Sqlite` + the bundled SQLitePCLRaw provider) is the source of truth for durable runtime state, vendored under `mdbase/lib/net8.0/` and loaded through mdbase-ps's own private `AssemblyLoadContext` loader (same pattern already established for JsonSchema.Net/Celly). This is explicitly sanctioned by Ch.13: *"Internal database rows and indexes do not need Markdown record contracts... Private storage tables remain implementation details but must preserve the same protocol evidence."*

## Considered Options

- **Markdown as source of truth, private store as index/cache only** — rejected. Keeps "files are the source of truth" as an absolute even for runtime state, but requires inventing a second coordination mechanism (lock file, WAL-like layer) over independent file writes to fake multi-file atomicity — strictly worse than using a real embedded database.
- **Pure Markdown, no private store** — rejected outright. No mechanism in #12's model achieves atomic multi-record transactions or generation-safe concurrent claims across independent files.

## Consequences

- Durable runtime is strictly **opt-in**: the SQLite dependency and durable-runtime machinery only load on explicit activation (e.g. `Enable-MdbRuntime`), never from `Connect-MdbCollection`. Matches Ch.13's "loading the collection remains safe and does not activate them" guarantee — `core_read`/`core_write`/`lifecycle` users never pay for or depend on SQLite.
- The database file lives at `.mdbase/runtime.sqlite3` inside the collection root, a new reserved/excluded path, default-on but host-configurable to an external path — keeps the durable store colocated and copy-with-the-folder.
- Markdown materialization of runtime records (`runs/`, `checkpoints/`, etc.) is a fully decoupled derived projection — rendered from SQLite state on-demand or via async best-effort sync, **never** inside the durable transaction. SQLite commit success is never gated on a filesystem write; Markdown is a rendering of the evidence, not a second copy of it.
- Concurrency mode is pinned as part of this decision (not deferred): WAL journal mode + `busy_timeout` retry, with connections opened per-operation rather than held for a run's lifetime — serves Ch.14's generation-safe concurrent-worker claiming directly.
- Out of scope here: actual table schemas for runs/leases/checkpoints/timers/action-attempts/dead-letters, the generation/CAS SQL patterns themselves, and the export-cmdlet design — deferred to the follow-up execution-protocol tickets this unblocks.

Decided via grilling on [#18](https://github.com/HeyItsGilbert/mdbase-ps/issues/18).
