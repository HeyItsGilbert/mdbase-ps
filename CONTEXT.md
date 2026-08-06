# mdbase-ps

A PowerShell-native automation surface for mdbase (v0.3) collections: cmdlets backed by a portable C# domain engine, not a CLI-parity wrapper.

## Language

**Core Engine**:
The portable C# library implementing mdbase's domain model, write pipeline, query engine, and durable runtime — `Mdbase.Core`. Owns file I/O, frontmatter parsing, and every mdbase behavior; has no PowerShell dependency and is usable from any .NET consumer.
_Avoid_: The module, the library (ambiguous with the PowerShell module), the engine (drop "core" only when context is unambiguous)

**Binding**:
A host-specific layer that exposes the Core Engine's behavior through a particular language or runtime's idioms. The PowerShell module (`mdbase`) is this project's binding — a thin cmdlet skin returning Core Engine objects onto the pipeline, formatted via `.format.ps1xml`/`.types.ps1xml`. Mirrors the spec's own distinction between a durable runtime (e.g. the Rust runtime) and a host binding (e.g. the Connect binding).
_Avoid_: Wrapper, adapter, client (client implies a network boundary; there isn't one)

**Module**:
Specifically the PowerShell binding — the `mdbase` PowerShell module distributed via PSGallery. Not the Core Engine.
_Avoid_: Library, package (ambiguous — the Core Engine is also a package)
