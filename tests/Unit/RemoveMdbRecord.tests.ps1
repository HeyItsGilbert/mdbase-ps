#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Remove-MdbRecord' {
    BeforeEach {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive "collection-$([guid]::NewGuid())")
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'removes a record with no backlinks and emits no warning' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Solo' } -Path 'solo.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root

        $warnings = $null
        Remove-MdbRecord -Collection $script:collection -Path 'solo.md' -Confirm:$false -WarningVariable warnings -WarningAction SilentlyContinue | Out-Null

        Test-Path (Join-Path $script:root 'solo.md') | Should -BeFalse
        $warnings.Count | Should -Be 0
    }

    It 'removes a record with backlinks, warning about them without blocking' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Target' } -Path 'target.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Source'; assignee = 'target.md' } -Path 'source.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root

        $warnings = $null
        Remove-MdbRecord -Collection $script:collection -Path 'target.md' -Confirm:$false -WarningVariable warnings -WarningAction SilentlyContinue | Out-Null

        Test-Path (Join-Path $script:root 'target.md') | Should -BeFalse
        $warnings.Count | Should -BeGreaterThan 0
        ($warnings -join ' ') | Should -Match 'source.md'
    }

    It '-WhatIf previews the would-be-deleted record without deleting it' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Keep' } -Path 'keep.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root

        $preview = Remove-MdbRecord -Collection $script:collection -Path 'keep.md' -WhatIf

        $preview.FileInfo.Path | Should -Be 'keep.md'
        Test-Path (Join-Path $script:root 'keep.md') | Should -BeTrue
    }
}
