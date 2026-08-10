function Get-MdbType {
    <#
    .SYNOPSIS
        Gets one or every loaded type from a connected mdbase collection's type registry.
    .DESCRIPTION
        -Name is identity-based lookup (case-insensitive, matching Mdbase.Core's own canonical
        name comparison); without it, every loaded MdbType is emitted onto the pipeline.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Name
        The type name to fetch, compared case-insensitively.
    .EXAMPLE
        PS> Get-MdbType -Collection $c -Name Task

        Returns the loaded 'Task' type.
    .OUTPUTS
        Mdbase.Core.MdbType
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.MdbType])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Position = 0)]
        [string]$Name
    )

    process {
        if (-not $PSBoundParameters.ContainsKey('Name')) {
            $Collection.Types.Values
            return
        }

        $canonical = $Name.ToLowerInvariant()
        $type = $null
        if ($Collection.Types.TryGetValue($canonical, [ref]$type)) {
            $type
            return
        }

        $diagnostic = [Mdbase.Core.MdbDiagnostic]@{
            Severity = [Mdbase.Core.MdbSeverity]::Error
            Code     = 'type_not_found'
            Message  = "No type named '$Name' is loaded in this collection."
            Type     = $Name
        }
        $errorRecord = [System.Management.Automation.ErrorRecord]::new(
            [System.Exception]::new($diagnostic.Message),
            $diagnostic.Code,
            [System.Management.Automation.ErrorCategory]::ObjectNotFound,
            $diagnostic)
        $errorRecord.ErrorDetails = [System.Management.Automation.ErrorDetails]::new($diagnostic.Message)
        $PSCmdlet.ThrowTerminatingError($errorRecord)
    }
}
