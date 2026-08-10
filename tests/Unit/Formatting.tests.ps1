#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Default formatting (.format.ps1xml)' {
    BeforeAll {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $collection = Connect-MdbCollection -Path $root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Alpha'; assignee = 'x.md' } -Path 'alpha.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $root
    }

    It 'renders MdbRecord with a Path/Types/Valid/Revision table' {
        $output = Get-MdbRecord -Collection $script:collection -Path 'alpha.md' | Format-Table | Out-String

        $output | Should -Match 'Path'
        $output | Should -Match 'Types'
        $output | Should -Match 'Valid'
        $output | Should -Match 'alpha\.md'
    }

    It 'renders MdbType with a Name/Version/FilePath table' {
        $output = Get-MdbType -Collection $script:collection -Name task | Format-Table | Out-String

        $output | Should -Match 'Name'
        $output | Should -Match 'FilePath'
        $output | Should -Match 'task'
    }

    It 'renders MdbQueryResult with a Path/Values table' {
        $output = Find-MdbRecord -Collection $script:collection | Format-Table | Out-String

        $output | Should -Match 'Path'
        $output | Should -Match 'alpha\.md'
    }

    It 'renders MdbQueryResultSet with a ResultCount/TotalCount list' {
        $output = Find-MdbRecord -Collection $script:collection -Raw | Format-List | Out-String

        $output | Should -Match 'ResultCount'
        $output | Should -Match 'TotalCount'
    }

    It 'renders MdbBacklinkEntry with a SourcePath/FieldPath/Target table' {
        New-MdbRecord -Collection $script:collection -Frontmatter @{ title = 'X' } -Path 'x.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $root

        $output = Get-MdbBacklink -Collection $script:collection -Path 'x.md' | Format-Table | Out-String

        $output | Should -Match 'SourcePath'
        $output | Should -Match 'alpha\.md'
    }
}
