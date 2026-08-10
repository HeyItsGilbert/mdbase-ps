#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Get-MdbBacklink' {
    BeforeAll {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $collection = Connect-MdbCollection -Path $root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Target' } -Path 'target.md' -Confirm:$false | Out-Null
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Source'; assignee = 'target.md' } -Path 'source.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $root
    }

    It 'returns every resolved incoming link for a target path' {
        $backlinks = Get-MdbBacklink -Collection $script:collection -Path 'target.md'

        $backlinks.Count | Should -Be 1
        $backlinks[0] | Should -BeOfType ([Mdbase.Core.Links.MdbBacklinkEntry])
        $backlinks[0].SourcePath | Should -Be 'source.md'
        $backlinks[0].FieldPath | Should -Be 'assignee'
    }

    It 'returns empty for a path with no incoming links' {
        $backlinks = Get-MdbBacklink -Collection $script:collection -Path 'source.md'

        $backlinks.Count | Should -Be 0
    }
}
