#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Get-MdbType' {
    BeforeAll {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')
        $script:collection = Connect-MdbCollection -Path $root
    }

    It 'returns the named type, matched case-insensitively' {
        $type = Get-MdbType -Collection $script:collection -Name 'TASK'

        $type | Should -BeOfType ([Mdbase.Core.MdbType])
        $type.Name | Should -Be 'task'
    }

    It 'throws a terminating not-found error for an unknown type name' {
        try {
            Get-MdbType -Collection $script:collection -Name 'does-not-exist' -ErrorAction Stop
        } catch {
            $_.TargetObject.Code | Should -Be 'type_not_found'
        }
    }

    It 'returns every loaded type when -Name is omitted' {
        $types = Get-MdbType -Collection $script:collection

        ($types | ForEach-Object Name) | Should -Contain 'task'
    }
}
