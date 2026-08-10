#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Initialize-MdbCollection' {
    It 'scaffolds mdbase.yaml plus the default types/contracts folders and returns a connected handle' {
        $root = Join-Path $TestDrive 'new-collection'

        $collection = Initialize-MdbCollection -Path $root -Confirm:$false

        $collection | Should -BeOfType ([Mdbase.Core.MdbCollection])
        Test-Path (Join-Path $root 'mdbase.yaml') | Should -BeTrue
        Test-Path (Join-Path $root '_types') -PathType Container | Should -BeTrue
        Test-Path (Join-Path $root '_contracts') -PathType Container | Should -BeTrue
    }

    It 'refuses to overwrite an existing mdbase.yaml without -Force' {
        $root = Join-Path $TestDrive 'existing-collection'
        Initialize-MdbCollection -Path $root -Confirm:$false | Out-Null

        { Initialize-MdbCollection -Path $root -Confirm:$false -ErrorAction Stop } | Should -Throw

        try {
            Initialize-MdbCollection -Path $root -Confirm:$false -ErrorAction Stop
        } catch {
            $_.TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
            $_.TargetObject.Code | Should -Be 'collection_already_initialized'
        }
    }

    It 'overwrites an existing mdbase.yaml with -Force' {
        $root = Join-Path $TestDrive 'force-collection'
        Initialize-MdbCollection -Path $root -Confirm:$false | Out-Null

        { Initialize-MdbCollection -Path $root -Force -Confirm:$false -ErrorAction Stop } | Should -Not -Throw
    }

    It 'supports -WhatIf without writing mdbase.yaml' {
        $root = Join-Path $TestDrive 'whatif-collection'

        Initialize-MdbCollection -Path $root -WhatIf

        Test-Path (Join-Path $root 'mdbase.yaml') | Should -BeFalse
    }
}
