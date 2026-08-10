#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
}

Describe 'Set-MdbRecord' {
    BeforeEach {
        $script:root = New-MdbFixtureCollection -Root (Join-Path $TestDrive "collection-$([guid]::NewGuid())")
        $collection = Connect-MdbCollection -Path $script:root
        New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Task'; status = 'open' } -Body 'Original body.' -Path 'task.md' -Confirm:$false | Out-Null
        $script:collection = Connect-MdbCollection -Path $script:root
    }

    It 'applies a -Patch to only the present keys' {
        $updated = Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ status = 'closed' } -Confirm:$false

        $updated.EffectiveFrontmatter['status'] | Should -Be 'closed'
        $updated.EffectiveFrontmatter['title'] | Should -Be 'Task'
        $updated.Body | Should -Be 'Original body.'
    }

    It '-Remove deletes a key outright' {
        Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ priority = 3 } -Confirm:$false | Out-Null
        $updated = Set-MdbRecord -Collection $script:collection -Path 'task.md' -Remove @('priority') -Confirm:$false

        $updated.Frontmatter.Contains('priority') | Should -BeFalse
    }

    It '-Document replaces the complete Markdown source' {
        $document = "---`ntitle: Replaced`nstatus: open`n---`nNew body.`n"

        $updated = Set-MdbRecord -Collection $script:collection -Path 'task.md' -Document $document -Confirm:$false

        $updated.EffectiveFrontmatter['title'] | Should -Be 'Replaced'
        $updated.Body.Trim() | Should -Be 'New body.'
    }

    It 'PowerShell parameter-set validation rejects combining -Patch and -Document' {
        { Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ status = 'closed' } -Document '---' -Confirm:$false } |
            Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
    }

    It 'throws concurrent_modification for a mismatched -IfRevision' {
        try {
            Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ status = 'closed' } -IfRevision 'sha256:0000000000000000000000000000000000000000000000000000000000000' -Confirm:$false -ErrorAction Stop
        } catch {
            $_.TargetObject.Code | Should -Be 'concurrent_modification'
        }
    }

    It 'succeeds when -IfRevision matches the current revision' {
        $current = Get-MdbRecord -Collection $script:collection -Path 'task.md'

        $updated = Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ status = 'closed' } -IfRevision $current.Revision -Confirm:$false

        $updated.EffectiveFrontmatter['status'] | Should -Be 'closed'
    }

    It '-WhatIf previews the update without persisting it' {
        $preview = Set-MdbRecord -Collection $script:collection -Path 'task.md' -Patch @{ status = 'closed' } -WhatIf

        $preview.EffectiveFrontmatter['status'] | Should -Be 'closed'
        (Get-Content (Join-Path $script:root 'task.md') -Raw) | Should -Match 'status:\s*open'
    }
}
