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
        try {
            New-MdbRecord -Collection $script:collection -Frontmatter @{ status = 'open' } -Path 'bad.md' -Confirm:$false -ErrorAction Stop
        } catch {
        }

        $Error[0].TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
        $Error[0].TargetObject.Severity | Should -Be ([Mdbase.Core.MdbSeverity]::Error)
        $Error[0].ErrorDetails.Message | Should -Not -BeNullOrEmpty
    }

    It 'synthesizes an invalid_query diagnostic for a malformed CEL expression that carries none' {
        try {
            Find-MdbRecord -Collection $script:collection -Where 'status ===' -ErrorAction Stop
        } catch {
        }

        $Error[0].TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
        $Error[0].TargetObject.Code | Should -Be 'invalid_query'
    }

    It 'synthesizes a collection_not_found diagnostic carrying the attempted path' {
        try {
            Connect-MdbCollection -Path (Join-Path $TestDrive 'nowhere') -ErrorAction Stop
        } catch {
        }

        $Error[0].TargetObject.Code | Should -Be 'collection_not_found'
        $Error[0].TargetObject.Path | Should -Not -BeNullOrEmpty
    }
}
