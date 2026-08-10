#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Get-MdbRecord' {
    BeforeAll {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $script:collection = Connect-MdbCollection -Path $script:root
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'One'; status = 'open' } -Path 'a.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'Two'; status = 'closed' } -Path 'b.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'returns the exact loaded record for a given path' {
        $record = Get-MdbRecord -Collection $script:collection -Path 'a.md'

        $record | Should -BeOfType ([Mdbase.Core.MdbRecord])
        $record.FileInfo.Path | Should -Be 'a.md'
        $record.EffectiveFrontmatter['title'] | Should -Be 'One'
    }

    It 'throws a terminating not-found error for a missing path' {
        { Get-MdbRecord -Collection $script:collection -Path 'missing.md' -ErrorAction Stop } | Should -Throw

        try {
            Get-MdbRecord -Collection $script:collection -Path 'missing.md' -ErrorAction Stop
        } catch {
            $_.TargetObject.Code | Should -Be 'record_not_found'
        }
    }

    It 'emits every record when -Path is omitted' {
        $records = Get-MdbRecord -Collection $script:collection

        $records.Count | Should -Be 2
        ($records.FileInfo.Path | Sort-Object) | Should -Be @('a.md', 'b.md')
    }
}
