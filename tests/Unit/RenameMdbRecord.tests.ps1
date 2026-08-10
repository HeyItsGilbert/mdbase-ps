#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Rename-MdbRecord' {
    BeforeEach {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive "collection-$([guid]::NewGuid())")
        $collection = Connect-MdbCollection -Path $script:root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Task' } -Path 'old.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'moves a record to the new path' {
        $renamed = Rename-MdbRecord -Collection $script:collection -Path 'old.md' -NewPath 'new.md' -Confirm:$false

        $renamed.FileInfo.Path | Should -Be 'new.md'
        Test-Path (Join-Path $script:root 'old.md') | Should -BeFalse
        Test-Path (Join-Path $script:root 'new.md') | Should -BeTrue
    }

    It 'throws path_conflict when the destination already exists' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Other' } -Path 'existing.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root

        try {
            Rename-MdbRecord -Collection $script:collection -Path 'old.md' -NewPath 'existing.md' -Confirm:$false -ErrorAction Stop
        } catch {
            $_.TargetObject.Code | Should -Be 'path_conflict'
        }
    }

    It '-WhatIf previews the rename without moving the file' {
        $preview = Rename-MdbRecord -Collection $script:collection -Path 'old.md' -NewPath 'preview.md' -WhatIf

        $preview.FileInfo.Path | Should -Be 'preview.md'
        Test-Path (Join-Path $script:root 'old.md') | Should -BeTrue
        Test-Path (Join-Path $script:root 'preview.md') | Should -BeFalse
    }
}
