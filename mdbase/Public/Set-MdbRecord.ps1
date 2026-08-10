function Set-MdbRecord {
    <#
    .SYNOPSIS
        Modifies an existing mdbase record.
    .DESCRIPTION
        Two mutually exclusive parameter sets map directly onto MdbCollection.Update's own
        patch/document distinction: PowerShell's parameter-set validation enforces the mutual
        exclusivity Mdbase.Core already enforces internally, instead of a manual runtime check.
        -WhatIf runs Mdbase.Core's own dryRun path, so the pipeline still gets the would-be
        MdbRecord as a preview; nothing is persisted.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Path
        Collection-relative path of the record to update.
    .PARAMETER Patch
        A set/null patch applied to only the present keys.
    .PARAMETER Remove
        Keys to delete outright, applied alongside -Patch.
    .PARAMETER Body
        A body override, applied alongside -Patch.
    .PARAMETER Document
        A complete replacement Markdown source; mutually exclusive with -Patch/-Remove/-Body.
    .PARAMETER IfRevision
        Optimistic-concurrency check against the record's current Revision.
    .EXAMPLE
        PS> Set-MdbRecord -Collection $c -Path tasks/fix-login.md -Patch @{ status = 'closed' }

        Sets 'status' to "closed", leaving every other field untouched.
    .OUTPUTS
        Mdbase.Core.MdbRecord
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium', DefaultParameterSetName = 'Patch')]
    [OutputType([Mdbase.Core.MdbRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(ParameterSetName = 'Patch')]
        [System.Collections.IDictionary]$Patch,

        [Parameter(ParameterSetName = 'Patch')]
        [string[]]$Remove,

        [Parameter(ParameterSetName = 'Patch')]
        [string]$Body,

        [Parameter(Mandatory, ParameterSetName = 'Document')]
        [string]$Document,

        [string]$IfRevision
    )

    process {
        # An unbound [string] parameter defaults to "" in PowerShell, never $null — Update's own
        # patch/document mutual-exclusivity check (and 'no override' vs. 'explicit empty value'
        # semantics) depends on genuinely receiving null for anything not supplied. PowerShell's
        # dynamic .NET method binder additionally coerces an explicit $null argument bound to a
        # `string` parameter back into "" (verified empirically) — routing a genuine null through
        # requires [NullString]::Value. These locals are named distinctly from -Body/-Document/
        # -IfRevision (PowerShell variable names are case-insensitive, so e.g. $body would
        # actually BE $Body — the same [string]-typed parameter slot — silently reintroducing
        # the null->"" coercion on assignment).
        $orderedPatch = if ($PSBoundParameters.ContainsKey('Patch')) { ConvertTo-MdbFrontmatter -Frontmatter $Patch } else { $null }
        $resolvedBody = if ($PSBoundParameters.ContainsKey('Body')) { $Body } else { $null }
        $resolvedDocument = if ($PSBoundParameters.ContainsKey('Document')) { $Document } else { $null }
        $resolvedIfRevision = if ($PSBoundParameters.ContainsKey('IfRevision')) { $IfRevision } else { $null }
        $bodyArg = if ($null -eq $resolvedBody) { [NullString]::Value } else { $resolvedBody }
        $documentArg = if ($null -eq $resolvedDocument) { [NullString]::Value } else { $resolvedDocument }
        $ifRevisionArg = if ($null -eq $resolvedIfRevision) { [NullString]::Value } else { $resolvedIfRevision }

        try {
            if ($PSCmdlet.ShouldProcess($Path, 'Update mdbase record')) {
                $Collection.Update($Path, $orderedPatch, $Remove, $bodyArg, $documentArg, $ifRevisionArg, $false)
            } elseif ($WhatIfPreference) {
                $Collection.Update($Path, $orderedPatch, $Remove, $bodyArg, $documentArg, $ifRevisionArg, $true)
            }
        } catch [Mdbase.Core.MdbWriteException] {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
        }
    }
}
