# Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/)
and this project adheres to [Semantic Versioning](http://semver.org/).

## [0.1.0] Unreleased

### Added

- PowerShell Binding: the full Core cmdlet surface (`Connect-MdbCollection`, `Initialize-MdbCollection`,
  `Get-MdbRecord`, `Find-MdbRecord`, `New-MdbRecord`, `Set-MdbRecord`, `Remove-MdbRecord`,
  `Rename-MdbRecord`, `Invoke-MdbBatch`, `Watch-MdbCollection`, `Get-MdbBacklink`, `Get-MdbType`),
  an `AssemblyLoadContext`-based loader that isolates `Mdbase.Core.dll`'s transitive dependencies,
  `mdbase.format.ps1xml` default views, and the psake `Build` wiring that publishes the Core
  Engine into `mdbase/lib/net8.0/` (#42).
- `tests/Unit/`: the first real Pester 5.4.0 coverage for the binding layer, exercised against
  real temp-directory fixture collections (no mocking of `Mdbase.Core`).

### Deferred

- `tests/Smoke/` (vendored `examples/v0.3/` collections exercised end-to-end through
  `Connect-MdbCollection`) — not populated in this pass; `tests/Unit/` already exercises every
  cmdlet against real fixtures, so this is a follow-on rather than a gap in binding-layer coverage.

