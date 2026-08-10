function Remove-MdbRecord {
    <#
    .SYNOPSIS
        Removes an mdbase record.
    .DESCRIPTION
        Reads MdbCollection.GetBacklinks for the target path first and writes a warning (not a
        block) when the record has incoming backlinks — deletion still proceeds; the caller is
        just told about the now-broken references. -WhatIf runs Mdbase.Core's own dryRun path,
        so the pipeline still gets the would-be-deleted MdbRecord as a preview.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Path
        Collection-relative path of the record to remove.
    .PARAMETER IfRevision
        Optimistic-concurrency check against the record's current Revision.
    .EXAMPLE
        PS> Remove-MdbRecord -Collection $c -Path tasks/fix-login.md

        Removes the record, warning first if anything still links to it.
    .OUTPUTS
        Mdbase.Core.MdbRecord
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    [OutputType([Mdbase.Core.MdbRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [string]$IfRevision
    )

    process {
        $backlinks = $Collection.GetBacklinks($Path)
        if ($backlinks.Count -gt 0) {
            $sources = ($backlinks | ForEach-Object { $_.SourcePath }) -join ', '
            Write-Warning "'$Path' has $($backlinks.Count) incoming link(s) that will be left unresolved: $sources"
        }

        # An unbound [string] parameter defaults to "" in PowerShell, never $null — Delete's
        # if_revision check treats null as "no concurrency check requested". PowerShell's dynamic
        # .NET method binder additionally coerces an explicit $null argument bound to a `string`
        # parameter back into "" (verified empirically) — routing a genuine null through requires
        # [NullString]::Value. This local is named distinctly from -IfRevision (PowerShell
        # variable names are case-insensitive, so $ifRevision would actually BE $IfRevision —
        # the same [string]-typed parameter slot — silently reintroducing the coercion).
        $resolvedIfRevision = if ($PSBoundParameters.ContainsKey('IfRevision')) { $IfRevision } else { $null }
        $ifRevisionArg = if ($null -eq $resolvedIfRevision) { [NullString]::Value } else { $resolvedIfRevision }

        try {
            if ($PSCmdlet.ShouldProcess($Path, 'Remove mdbase record')) {
                $Collection.Delete($Path, $ifRevisionArg, $false)
            } elseif ($WhatIfPreference) {
                $Collection.Delete($Path, $ifRevisionArg, $true)
            }
        } catch [Mdbase.Core.MdbWriteException] {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
        }
    }
}
