#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Connect-MdbCollection' {
    It 'returns a connected MdbCollection handle for a real collection' {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection')

        $collection = Connect-MdbCollection -Path $root

        $collection | Should -BeOfType ([Mdbase.Core.MdbCollection])
        $collection.RootPath | Should -Be (Resolve-Path $root).Path
        $collection.Types.ContainsKey('task') | Should -BeTrue
    }

    It 'accepts pipeline input by property name (FullName)' {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection-pipeline')

        $collection = Get-Item $root | Connect-MdbCollection

        $collection.RootPath | Should -Be (Resolve-Path $root).Path
    }

    It 'throws a clear, catchable terminating error for a directory with no mdbase.yaml' {
        $emptyDir = Join-Path $TestDrive 'not-a-collection'
        New-Item -Path $emptyDir -ItemType Directory -Force | Out-Null

        { Connect-MdbCollection -Path $emptyDir -ErrorAction Stop } | Should -Throw

        try {
            Connect-MdbCollection -Path $emptyDir -ErrorAction Stop
        } catch {
            $_.TargetObject | Should -BeOfType ([Mdbase.Core.MdbDiagnostic])
            $_.TargetObject.Code | Should -Be 'collection_not_found'
        }
    }
}
