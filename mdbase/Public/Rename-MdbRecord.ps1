function Rename-MdbRecord {
    <#
    .SYNOPSIS
        Moves an mdbase record to a new collection-relative path.
    .DESCRIPTION
        A thin wrapper over MdbCollection.Rename. Does not rewrite link text in any other
        record. -WhatIf runs Mdbase.Core's own dryRun path, so the pipeline still gets the
        would-be-renamed MdbRecord as a preview; nothing is persisted.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Path
        Collection-relative path of the record to rename.
    .PARAMETER NewPath
        The destination collection-relative path.
    .PARAMETER IfRevision
        Optimistic-concurrency check against the record's current Revision.
    .EXAMPLE
        PS> Rename-MdbRecord -Collection $c -Path tasks/fix-login.md -NewPath tasks/fix-login-page.md

        Moves the record to its new path.
    .OUTPUTS
        Mdbase.Core.MdbRecord
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    [OutputType([Mdbase.Core.MdbRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [Parameter(Mandatory, Position = 1)]
        [string]$NewPath,

        [string]$IfRevision
    )

    process {
        # An unbound [string] parameter defaults to "" in PowerShell, never $null — Rename's
        # if_revision check treats null as "no concurrency check requested". PowerShell's dynamic
        # .NET method binder additionally coerces an explicit $null argument bound to a `string`
        # parameter back into "" (verified empirically) — routing a genuine null through requires
        # [NullString]::Value. This local is named distinctly from -IfRevision (PowerShell
        # variable names are case-insensitive, so $ifRevision would actually BE $IfRevision —
        # the same [string]-typed parameter slot — silently reintroducing the coercion).
        $resolvedIfRevision = if ($PSBoundParameters.ContainsKey('IfRevision')) { $IfRevision } else { $null }
        $ifRevisionArg = if ($null -eq $resolvedIfRevision) { [NullString]::Value } else { $resolvedIfRevision }

        try {
            if ($PSCmdlet.ShouldProcess("$Path -> $NewPath", 'Rename mdbase record')) {
                $Collection.Rename($Path, $NewPath, $ifRevisionArg, $false)
            } elseif ($WhatIfPreference) {
                $Collection.Rename($Path, $NewPath, $ifRevisionArg, $true)
            }
        } catch [Mdbase.Core.MdbWriteException] {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
        }
    }
}
