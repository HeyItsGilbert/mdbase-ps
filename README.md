# mdbase

A PowerShell-native automation surface for [mdbase](https://mdbase.dev) (v0.3) collections —
cmdlets backed by a portable C# domain engine, not a CLI-parity wrapper.

## Overview

mdbase-ps has two parts (see [ADR-0002](docs/adr/0002-csharp-core-engine-powershell-binding.md)):

- **Core Engine** (`Mdbase.Core`, `src/Mdbase.Core/`): a portable C# library implementing
  mdbase's domain model, write pipeline, query engine, and link/backlink index. It owns every
  bit of mdbase behavior — file I/O, frontmatter parsing, JSON Schema validation, CEL
  expression evaluation — with no PowerShell dependency.
- **Binding** (`mdbase`, `mdbase/`): the PowerShell module in this repo. A thin cmdlet skin
  that calls into the Core Engine and returns its objects directly onto the pipeline, formatted
  via `mdbase.format.ps1xml`. `Mdbase.Core.dll` loads through a private
  `AssemblyLoadContext`-isolated dependency set, so mdbase-ps never collides with another
  module (or your own script) loading a different version of the same library.

### Cmdlets

| Cmdlet | Purpose |
| --- | --- |
| `Connect-MdbCollection` | Open an existing collection |
| `Initialize-MdbCollection` | Scaffold a brand-new collection |
| `Get-MdbRecord` | Fetch one record by path, or every record |
| `Find-MdbRecord` | Run a `where`/`select`/`order_by`/`group_by`/`summaries` query |
| `New-MdbRecord` | Create a record |
| `Set-MdbRecord` | Update a record (patch or full-document replacement) |
| `Remove-MdbRecord` | Delete a record |
| `Rename-MdbRecord` | Move a record to a new path |
| `Invoke-MdbBatch` | Run an ordered batch of create/update/delete/rename operations |
| `Watch-MdbCollection` | Stream (or react to) filesystem changes, debounced per-path |
| `Get-MdbBacklink` | List every resolved incoming link to a record |
| `Get-MdbType` | Inspect the loaded type registry |

Every write cmdlet (`New-`/`Set-`/`Remove-`/`Rename-MdbRecord`) supports `-WhatIf`/`-Confirm`,
and every cmdlet's terminating errors carry the engine's structured diagnostic on
`$Error[0].TargetObject` — ordinary `try`/`catch`, not envelope-parsing.

## Installation

mdbase-ps isn't published to the PSGallery yet. Build it from source:

**Prerequisites:** [PowerShell 7+](https://github.com/PowerShell/PowerShell), the
[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/HeyItsGilbert/mdbase-ps.git
cd mdbase-ps
./build.ps1 -Bootstrap -Task Build
Import-Module ./Output/mdbase/<version>/mdbase.psd1
```

`-Bootstrap` installs the psake/PowerShellBuild/Pester toolchain the build needs; the `Build`
task publishes `Mdbase.Core.dll` (via `dotnet publish`) into `mdbase/lib/net8.0/` before staging
the module to `Output/`. For local development against the module source tree directly (no
build step beyond publishing the Core Engine once):

```powershell
dotnet publish src/Mdbase.Core -f net8.0 --no-self-contained -o mdbase/lib/net8.0
Import-Module ./mdbase/mdbase.psd1
```

## Examples

```powershell
# Open a collection
$c = Connect-MdbCollection -Path ./my-notes

# Scaffold a new one
$c = Initialize-MdbCollection -Path ./new-collection

# Fetch and query records
Get-MdbRecord -Collection $c -Path tasks/fix-login.md
Find-MdbRecord -Collection $c -Where 'status == "open"' -OrderBy priority:desc

# Create, update, and remove records
New-MdbRecord -Collection $c -Frontmatter @{ title = 'Fix login'; status = 'open' } -Types task -Path tasks/fix-login.md
Set-MdbRecord -Collection $c -Path tasks/fix-login.md -Patch @{ status = 'closed' }
Remove-MdbRecord -Collection $c -Path tasks/fix-login.md

# Batch changes and preview with -WhatIf
Invoke-MdbBatch -Collection $c -Operation @(
    @{ Kind = 'Update'; Path = 'tasks/a.md'; Patch = @{ status = 'closed' } }
    @{ Kind = 'Delete'; Path = 'tasks/b.md' }
)
New-MdbRecord -Collection $c -Frontmatter @{ title = 'Preview' } -Path tasks/preview.md -WhatIf

# Watch for changes
Watch-MdbCollection -Collection $c | Where-Object { $_ -is [Mdbase.Core.MdbRecord] }

# Explore links and types
Get-MdbBacklink -Collection $c -Path people/alice.md
Get-MdbType -Collection $c -Name task
```

See `docs/` (architecture, domain vocabulary, ADRs) and each cmdlet's own comment-based help
(`Get-Help New-MdbRecord -Full`) for more.
