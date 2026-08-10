# Shared fixture/import helper for tests/Unit/*.tests.ps1 (#42's Testing Decisions: real
# temp-directory fixture collections, never mocks of Mdbase.Core). Dot-sourced from each test
# file's BeforeAll/BeforeDiscovery.

function Import-MdbaseModuleUnderTest {
    <#
    .SYNOPSIS
        Imports the mdbase module from source (not Output/) so tests/Unit exercises exactly
        what a developer's working tree contains.
    #>
    $modulePath = Join-Path -Path $PSScriptRoot -ChildPath '../../mdbase/mdbase.psd1'
    Get-Module -Name mdbase | Remove-Module -Force -ErrorAction SilentlyContinue
    Import-Module -Name $modulePath -Force -ErrorAction Stop
}

function New-MdbFixtureCollection {
    <#
    .SYNOPSIS
        Scaffolds a real temp-directory mdbase collection with one 'task' type, returning its
        root path. Every tests/Unit spec builds its own fixture — no shared mutable state.
    .PARAMETER Root
        Destination directory; created if missing. Typically a path under TestDrive:.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Root
    )

    New-Item -Path $Root -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $Root '_types') -ItemType Directory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Root 'mdbase.yaml') -Value "spec_version: `"0.3`"`n" -NoNewline

    $taskType = @'
---
kind: mdbase.type
name: task
version: 1
match:
  fields_present: [title]
schema:
  dialect: json-schema-2020-12
  value:
    type: object
    properties:
      title: { type: string }
      status: { type: string }
      priority: { type: integer }
      assignee: { type: string }
    required: [title]
collection:
  links:
    assignee:
      target_type: any
      validate_exists: false
---
'@
    Set-Content -LiteralPath (Join-Path $Root '_types/task.md') -Value $taskType -NoNewline

    $Root
}
