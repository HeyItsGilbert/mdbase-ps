function Invoke-MdbBatch {
    <#
    .SYNOPSIS
        Runs an ordered batch of create/update/delete/rename operations against a collection.
    .DESCRIPTION
        Each input hashtable (Kind plus that kind's fields) maps to one MdbBatchOperation
        factory call, then MdbCollection.ExecuteBatch. Never throws for a per-operation failure
        — one PowerShell object per MdbBatchOperationResult (Valid/Path/Result/Diagnostics) is
        emitted instead, so scripted bulk changes get the spec's own envelope shape rather than
        N separate terminating-error try/catches.

        Without -AllowPartial, every operation is validated (each already-validated operation's
        effect visible to the next operation's own uniqueness/path checks) before any of them
        persists; the whole batch aborts on the first invalid operation. -AllowPartial instead
        validates-and-writes each operation independently, continuing past individual failures.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Operation
        One hashtable per operation. Every entry requires 'Kind' (Create/Update/Delete/Rename)
        plus that kind's own fields: Create (Frontmatter/Body/Types/Path), Update
        (Path/Patch/Remove/Body/Document/IfRevision), Delete (Path/IfRevision), Rename
        (Path/NewPath/IfRevision).
    .PARAMETER AllowPartial
        Validates and writes each operation independently, continuing past individual failures,
        instead of validating the whole batch before any of it persists.
    .EXAMPLE
        PS> Invoke-MdbBatch -Collection $c -Operation @(
                @{ Kind = 'Update'; Path = 'tasks/a.md'; Patch = @{ status = 'closed' } }
                @{ Kind = 'Delete'; Path = 'tasks/b.md' }
            )

        Closes one task and deletes another as a single validated batch.
    .OUTPUTS
        Mdbase.Core.Write.MdbBatchOperationResult
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    [OutputType([Mdbase.Core.Write.MdbBatchOperationResult])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory)]
        [hashtable[]]$Operation,

        [switch]$AllowPartial
    )

    process {
        $operations = [System.Collections.Generic.List[Mdbase.Core.Write.MdbBatchOperation]]::new()
        foreach ($entry in $Operation) {
            $operations.Add((ConvertTo-MdbBatchOperationInternal -Operation $entry))
        }

        if ($PSCmdlet.ShouldProcess("$($operations.Count) operation(s)", 'Execute mdbase batch')) {
            $Collection.ExecuteBatch($operations.AsReadOnly(), [bool]$AllowPartial)
        }
    }
}
