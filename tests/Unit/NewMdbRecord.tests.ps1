#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'New-MdbRecord' {
    BeforeEach {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive "collection-$([guid]::NewGuid())")
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'creates a record and returns the new MdbRecord' {
        $record = New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Fix login'; status = 'open' } -Path 'tasks/fix-login.md' -Confirm:$false

        $record | Should -BeOfType ([Mdbase.Core.MdbRecord])
        $record.IsValid | Should -BeTrue
        Test-Path (Join-Path $script:root 'tasks/fix-login.md') | Should -BeTrue
    }

    It 'creates a record with an explicit body' {
        $record = New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'With body' } -Body 'Hello body.' -Path 'a.md' -Confirm:$false

        $record.Body | Should -Be 'Hello body.'
    }

    It 'throws a terminating error carrying the schema diagnostic on validation failure' {
        { New-MdbRecord -Collection $script:collection -Frontmatter @{ status = 'open' } -Types task -Path 'bad.md' -Confirm:$false -ErrorAction Stop } | Should -Throw

        try {
            New-MdbRecord -Collection $script:collection -Frontmatter @{ status = 'open' } -Types task -Path 'bad.md' -Confirm:$false -ErrorAction Stop
        } catch {
            $_.TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
            $_.TargetObject.Code | Should -Be 'schema_required'
        }
    }

    It 'throws path_conflict when the target path already exists' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'First' } -Path 'dup.md' -Confirm:$false | Out-Null

        try {
            New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Second' } -Path 'dup.md' -Confirm:$false -ErrorAction Stop
        } catch {
            $_.TargetObject.Code | Should -Be 'path_conflict'
        }
    }

    It '-WhatIf previews the would-be record without writing a file' {
        $preview = New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Preview' } -Path 'preview.md' -WhatIf

        $preview | Should -BeOfType ([Mdbase.Core.MdbRecord])
        $preview.FileInfo.Path | Should -Be 'preview.md'
        Test-Path (Join-Path $script:root 'preview.md') | Should -BeFalse
    }
}
