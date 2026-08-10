function Connect-MdbCollection {
    <#
    .SYNOPSIS
        Connects to an existing mdbase collection.
    .DESCRIPTION
        Runs Mdbase.Core's full three-phase load (contracts, types, records+links) and returns a
        connected handle every other mdbase cmdlet accepts via -Collection — there is no ambient
        or global collection state (spec ADR-0002).
    .PARAMETER Path
        Directory containing the collection's 'mdbase.yaml'.
    .EXAMPLE
        PS> $c = Connect-MdbCollection -Path ./my-collection

        Connects to the collection rooted at './my-collection'.
    .OUTPUTS
        Mdbase.Core.MdbCollection
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.MdbCollection])]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [Alias('FullName')]
        [string]$Path
    )

    process {
        try {
            [Mdbase.Core.MdbCollection]::Connect($Path)
        } catch [Mdbase.Core.MdbCollectionNotFoundException] {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
        }
    }
}
