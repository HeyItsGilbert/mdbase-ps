#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
}

Describe 'Module import' {
    BeforeAll {
        Import-MdbaseModuleUnderTest
    }

    AfterAll {
        Get-Module -Name mdbase | Remove-Module -Force -ErrorAction SilentlyContinue
    }

    It 'loads Mdbase.Core.dll and exports every Core cmdlet' {
        $expected = @(
            'Connect-MdbCollection', 'Initialize-MdbCollection', 'Get-MdbRecord', 'Find-MdbRecord',
            'New-MdbRecord', 'Set-MdbRecord', 'Remove-MdbRecord', 'Rename-MdbRecord',
            'Invoke-MdbBatch', 'Watch-MdbCollection', 'Get-MdbBacklink', 'Get-MdbType'
        )
        $exported = (Get-Command -Module mdbase).Name
        foreach ($name in $expected) {
            $exported | Should -Contain $name
        }
    }

    It 'resolves Mdbase.Core types for cmdlet parameter binding' {
        { [Mdbase.Core.MdbCollection] } | Should -Not -Throw
    }

    It 'loads Mdbase.Core.dll into the default AssemblyLoadContext (so [TypeName] resolution works)' {
        $assembly = [Mdbase.Core.MdbCollection].Assembly
        $context = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext($assembly)
        $context | Should -Be ([System.Runtime.Loader.AssemblyLoadContext]::Default)
    }

    It 'isolates Mdbase.Core transitive dependencies into a private AssemblyLoadContext' {
        $yamlDotNet = [System.Runtime.Loader.AssemblyLoadContext]::All |
            Where-Object { $_.Name -eq 'mdbase-core-dependencies' } |
            ForEach-Object { $_.Assemblies } |
            Where-Object { $_.GetName().Name -eq 'YamlDotNet' }
        $yamlDotNet | Should -Not -BeNullOrEmpty

        $context = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext($yamlDotNet)
        $context.Name | Should -Be 'mdbase-core-dependencies'
        $context | Should -Not -Be ([System.Runtime.Loader.AssemblyLoadContext]::Default)
    }

    It 'removes cleanly with Get-Module -Remove' {
        { Get-Module -Name mdbase | Remove-Module -Force } | Should -Not -Throw
        Get-Module -Name mdbase | Should -BeNullOrEmpty
    }
}
