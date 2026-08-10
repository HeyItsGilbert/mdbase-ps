function Get-MdbRecord {
    <#
    .SYNOPSIS
        Gets one or every loaded record from a connected mdbase collection.
    .DESCRIPTION
        -Path is identity-based lookup: the exact loaded MdbRecord for that path, or a
        terminating 'record_not_found' error. Without -Path, every indexed record is emitted
        onto the pipeline one at a time — enumerating everything is not a separate cmdlet.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Path
        Collection-relative path of the record to fetch.
    .EXAMPLE
        PS> Get-MdbRecord -Collection $c -Path tasks/fix-login.md

        Returns the loaded record at that path.
    .EXAMPLE
        PS> Get-MdbRecord -Collection $c

        Emits every loaded record.
    .OUTPUTS
        Mdbase.Core.MdbRecord
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.MdbRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [Parameter(Position = 0)]
        [string]$Path
    )

    process {
        if (-not $PSBoundParameters.ContainsKey('Path')) {
            $Collection.Records.Values
            return
        }

        $normalized = $Path.Replace('\', '/').TrimStart('/')
        $record = $null
        if ($Collection.Records.TryGetValue($normalized, [ref]$record)) {
            $record
            return
        }

        $diagnostic = [Mdbase.Core.MdbDiagnostic]@{
            Severity = [Mdbase.Core.MdbSeverity]::Error
            Code     = 'record_not_found'
            Message  = "No record exists at '$normalized'."
            Path     = $normalized
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
