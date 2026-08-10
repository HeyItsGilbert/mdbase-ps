#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Error translation' {
    BeforeAll {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $script:collection = Connect-MdbCollection -Path $root
    }

    It 'preserves the MdbDiagnostic on $Error[0].TargetObject for a write failure' {
        $caught = $null
        try {
            New-MdbRecord -Collection $script:collection -Frontmatter @{ status = 'open' } -Types task -Path 'bad.md' -Confirm:$false -ErrorAction Stop
        } catch {
            $caught = $_
        }

        # Asserts against the directly-caught ErrorRecord, not the ambient $Error collection —
        # $Error is a session-global list shared across every Pester container in the same
        # process, so its [0] entry is not reliably *this* test's error once other test files
        # run alongside this one.
        $caught.TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
        $caught.TargetObject.Severity | Should -Be ([Mdbase.Core.MdbSeverity]::Error)
        $caught.ErrorDetails.Message | Should -Not -BeNullOrEmpty
    }

    It 'synthesizes an invalid_query diagnostic for a malformed CEL expression that carries none' {
        $caught = $null
        try {
            Find-MdbRecord -Collection $script:collection -Where 'status ===' -ErrorAction Stop
        } catch {
            $caught = $_
        }

        $caught.TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
        $caught.TargetObject.Code | Should -Be 'invalid_query'
    }

    It 'synthesizes a collection_not_found diagnostic carrying the attempted path' {
        $caught = $null
        try {
            Connect-MdbCollection -Path (Join-Path $TestDrive 'nowhere') -ErrorAction Stop
        } catch {
            $caught = $_
        }

        $caught.TargetObject.Code | Should -Be 'collection_not_found'
        $caught.TargetObject.Path | Should -Not -BeNullOrEmpty
    }
}
