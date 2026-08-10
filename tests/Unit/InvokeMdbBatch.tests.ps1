#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Invoke-MdbBatch' {
    BeforeEach {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive "collection-$([guid]::NewGuid())")
        $collection = Connect-MdbCollection -Path $script:root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'A'; status = 'open' } -Path 'a.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'B'; status = 'open' } -Path 'b.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'runs create/update/delete/rename operations and emits one result per operation' {
        $results = Invoke-MdbBatch -Collection $script:collection -Confirm:$false -Operation @(
            @{ Kind = 'Create'; Frontmatter = @{ title = 'C' }; Path = 'c.md' }
            @{ Kind = 'Update'; Path = 'a.md'; Patch = @{ status = 'closed' } }
            @{ Kind = 'Rename'; Path = 'b.md'; NewPath = 'b2.md' }
            @{ Kind = 'Delete'; Path = 'c.md' }
        )

        $results.Count | Should -Be 4
        $results | ForEach-Object { $_.Valid | Should -BeTrue }
        (Test-Path (Join-Path $script:root 'b2.md')) | Should -BeTrue
        (Test-Path (Join-Path $script:root 'c.md')) | Should -BeFalse
    }

    It 'without -AllowPartial, aborts the whole batch on the first invalid operation, persisting nothing' {
        $results = Invoke-MdbBatch -Collection $script:collection -Confirm:$false -Operation @(
            @{ Kind = 'Update'; Path = 'a.md'; Patch = @{ status = 'closed' } }
            @{ Kind = 'Update'; Path = 'does-not-exist.md'; Patch = @{ status = 'closed' } }
            @{ Kind = 'Update'; Path = 'b.md'; Patch = @{ status = 'closed' } }
        )

        $results.Count | Should -Be 3
        # op[0] preflights fine on its own — it's the batch that never commits, since op[1] fails.
        $results[0].Valid | Should -BeTrue
        $results[1].Valid | Should -BeFalse
        $results[1].Diagnostics[0].Code | Should -Be 'record_not_found'
        $results[2].Valid | Should -BeFalse

        # Nothing persisted — including op[0], which only preflighted, never wrote.
        $updated = Get-MdbRecord -Collection (Connect-MdbCollection -Path $script:root) -Path 'a.md'
        $updated.EffectiveFrontmatter['status'] | Should -Be 'open'
    }

    It '-AllowPartial validates-and-writes each operation independently' {
        $results = Invoke-MdbBatch -Collection $script:collection -AllowPartial -Confirm:$false -Operation @(
            @{ Kind = 'Update'; Path = 'a.md'; Patch = @{ status = 'closed' } }
            @{ Kind = 'Update'; Path = 'does-not-exist.md'; Patch = @{ status = 'closed' } }
            @{ Kind = 'Update'; Path = 'b.md'; Patch = @{ status = 'closed' } }
        )

        $results[0].Valid | Should -BeTrue
        $results[1].Valid | Should -BeFalse
        $results[1].Diagnostics[0].Code | Should -Be 'record_not_found'
        $results[2].Valid | Should -BeTrue

        $updated = Get-MdbRecord -Collection (Connect-MdbCollection -Path $script:root) -Path 'a.md'
        $updated.EffectiveFrontmatter['status'] | Should -Be 'closed'
    }
}
