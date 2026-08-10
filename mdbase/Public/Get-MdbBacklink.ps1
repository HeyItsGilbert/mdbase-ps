function Get-MdbBacklink {
    <#
    .SYNOPSIS
        Gets every resolved incoming link that targets a record.
    .DESCRIPTION
        A thin wrapper over MdbCollection.GetBacklinks — "what links here" without hand-walking
        every other record's own Links.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Path
        Collection-relative path of the target record.
    .EXAMPLE
        PS> Get-MdbBacklink -Collection $c -Path people/alice.md

        Every record with a resolved, non-ambiguous link pointing at 'people/alice.md'.
    .OUTPUTS
        Mdbase.Core.Links.MdbBacklinkEntry
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.Links.MdbBacklinkEntry])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Mandatory, Position = 0)]
        [string]$Path
    )

    process {
        $Collection.GetBacklinks($Path)
    }
}
