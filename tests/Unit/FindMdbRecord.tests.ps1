#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Find-MdbRecord' {
    BeforeAll {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $collection = Connect-MdbCollection -Path $root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Alpha'; status = 'open'; priority = 2 } -Path 'a.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Beta'; status = 'open'; priority = 0 } -Path 'b.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Gamma'; status = 'closed'; priority = 1 } -Path 'c.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $root
    }

    It 'filters with -Where' {
        $results = Find-MdbRecord -Collection $script:collection -Where 'status == "open"'

        $results.Count | Should -Be 2
    }

    It 'projects named expressions with a hashtable -Select' {
        $results = Find-MdbRecord -Collection $script:collection -Where 'status == "open"' -Select @{ upper = 'title.upperAscii()' }

        ($results.Values.upper | Sort-Object) | Should -Be @('ALPHA', 'BETA')
    }

    It 'projects field names with a string-array -Select' {
        $results = Find-MdbRecord -Collection $script:collection -Where 'file.path == "a.md"' -Select @('title')

        $results[0].Values['title'] | Should -Be 'Alpha'
    }

    It 'orders with -OrderBy field:desc syntax' {
        $results = Find-MdbRecord -Collection $script:collection -OrderBy 'priority:desc'

        $results[0].EffectiveFrontmatter['priority'] | Should -Be 2
        $results[-1].EffectiveFrontmatter['priority'] | Should -Be 0
    }

    It 'groups and summarizes with -GroupBy/-Summaries under -Raw' {
        $resultSet = Find-MdbRecord -Collection $script:collection -GroupBy 'status' -Summaries @{ total = 'priority:sum' } -Raw

        $resultSet | Should -BeOfType ([Mdbase.Core.Query.MdbQueryResultSet])
        $resultSet.Meta.Groups.Count | Should -Be 2
        $openGroup = $resultSet.Meta.Groups | Where-Object { $_.Values['status'] -eq 'open' }
        $openGroup.Summaries['total'] | Should -Be 2
    }

    It 'paginates with -Limit and reports -Raw Meta.TotalCount/HasMore' {
        $resultSet = Find-MdbRecord -Collection $script:collection -Limit 1 -OrderBy 'title' -Raw

        $resultSet.Results.Count | Should -Be 1
        $resultSet.Meta.TotalCount | Should -Be 3
        $resultSet.Meta.HasMore | Should -BeTrue
    }

    It 'resolves -Context into context.this' {
        $resultSet = Find-MdbRecord -Collection $script:collection -Context 'a.md' -Raw

        $resultSet.Meta.Context | Should -Be 'a.md'
    }

    It 'returns every unaffected result and warns for a per-record evaluation error' {
        $warnings = $null
        $results = Find-MdbRecord -Collection $script:collection -Select @{ ratio = '10 / priority' } -WarningVariable warnings -WarningAction SilentlyContinue

        $results.Count | Should -Be 3
        $failed = $results | Where-Object { $_.FileInfo.Path -eq 'b.md' }
        $failed.Values['ratio'] | Should -BeNullOrEmpty
        $warnings.Count | Should -BeGreaterThan 0
        ($warnings -join ' ') | Should -Match 'selection_error'
    }
}
