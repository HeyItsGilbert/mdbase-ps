function New-MdbRecord {
    <#
    .SYNOPSIS
        Creates a new mdbase record.
    .DESCRIPTION
        A thin wrapper over MdbCollection.Create. -WhatIf runs Mdbase.Core's own dryRun path —
        Core's validation/lifecycle/path-generation all still run, so the pipeline still gets the
        would-be MdbRecord as a preview, alongside PowerShell's usual "What if:" message; nothing
        is persisted. Every hard failure (schema, path_conflict, unique_conflict, etc.) is a
        terminating error carrying the engine's diagnostic — ordinary try/catch, no envelope to parse.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Frontmatter
        The record's input frontmatter mapping.
    .PARAMETER Body
        The record's Markdown body, after the frontmatter block.
    .PARAMETER Types
        Explicit type membership, taking precedence over the frontmatter's own explicit type
        key(s) and inferred matching.
    .PARAMETER Path
        An explicit target path; omitted, it is generated from the matched types'
        collection.path.pattern.
    .EXAMPLE
        PS> New-MdbRecord -Collection $c -Frontmatter @{ title = 'Fix login'; status = 'open' } -Types task

        Creates a new record matched to the 'task' type.
    .OUTPUTS
        Mdbase.Core.MdbRecord
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    [OutputType([Mdbase.Core.MdbRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Frontmatter,

        [string]$Body,

        [string[]]$Types,

        [string]$Path
    )

    process {
        $orderedFrontmatter = ConvertTo-MdbFrontmatter -Frontmatter $Frontmatter
        # An unbound [string] parameter defaults to "" in PowerShell, never $null — Mdbase.Core
        # distinguishes "no body override"/"generate the path" (null) from an explicit empty
        # value. PowerShell's dynamic .NET method binder additionally coerces an explicit $null
        # argument bound to a `string` parameter back into "" (verified empirically) — routing a
        # genuine null through requires [NullString]::Value. These locals are named distinctly
        # from -Body/-Path (PowerShell variable names are case-insensitive, so e.g. $body would
        # actually BE $Body — the same [string]-typed parameter slot — silently reintroducing
        # the null->"" coercion on assignment).
        $resolvedBody = if ($PSBoundParameters.ContainsKey('Body')) { $Body } else { $null }
        $resolvedPath = if ($PSBoundParameters.ContainsKey('Path')) { $Path } else { $null }
        $bodyArg = if ($null -eq $resolvedBody) { [NullString]::Value } else { $resolvedBody }
        $pathArg = if ($null -eq $resolvedPath) { [NullString]::Value } else { $resolvedPath }
        $target = if ($null -ne $resolvedPath) { $resolvedPath } else { '(new record)' }

        try {
            if ($PSCmdlet.ShouldProcess($target, 'Create mdbase record')) {
                $Collection.Create($orderedFrontmatter, $bodyArg, $Types, $pathArg, $false)
            } elseif ($WhatIfPreference) {
                $Collection.Create($orderedFrontmatter, $bodyArg, $Types, $pathArg, $true)
            }
        } catch [Mdbase.Core.MdbWriteException] {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
        }
    }
}
