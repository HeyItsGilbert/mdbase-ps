function Find-MdbRecord {
    <#
    .SYNOPSIS
        Runs an MdbQuery against a connected mdbase collection.
    .DESCRIPTION
        Exposes every MdbQuery capability as PowerShell parameters accepting plain strings for
        CEL expressions. Emits the query's Results by default; -Raw instead returns the full
        MdbQueryResultSet (including Meta/Diagnostics) for grouped/summarized queries. Each
        result-set diagnostic (a where/projection/summary evaluation error on one record) is
        written to the warning stream rather than suppressing the rest of the results.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER Types
        OR-filter by matched-type membership; every record is a candidate when omitted.
    .PARAMETER Where
        A CEL predicate source string; every candidate passes when omitted.
    .PARAMETER Select
        Either a string array of field names, or a hashtable of name -> CEL expression.
    .PARAMETER OrderBy
        Field references as "field" or "field:asc"/"field:desc" (default ascending).
    .PARAMETER GroupBy
        Field references, same "field[:asc|desc]" shape as -OrderBy.
    .PARAMETER Summaries
        A hashtable of result-name -> "field:function".
    .PARAMETER Context
        Collection-relative path resolved once into `context.this`.
    .PARAMETER FrontmatterMode
        Which frontmatter member(s) serialize into each result: Effective (default), Persisted, or Both.
    .PARAMETER Raw
        Emits the full MdbQueryResultSet (Results/Meta/Diagnostics) instead of just Results.
    .EXAMPLE
        PS> Find-MdbRecord -Collection $c -Where 'status == "open"'

        Every record whose effective 'status' field is "open".
    .OUTPUTS
        Mdbase.Core.Query.MdbQueryResult, or Mdbase.Core.Query.MdbQueryResultSet with -Raw.
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.Query.MdbQueryResult])]
    [OutputType([Mdbase.Core.Query.MdbQueryResultSet])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [string[]]$Types,

        [string]$Where,

        [object]$Select,

        [string[]]$OrderBy,

        [string[]]$GroupBy,

        [hashtable]$Summaries,

        [Nullable[int]]$Limit,

        [Nullable[int]]$Offset,

        [string]$Context,

        [ValidateSet('Effective', 'Persisted', 'Both')]
        [string]$FrontmatterMode = 'Effective',

        [switch]$IncludeBody,

        [switch]$Raw
    )

    process {
        try {
            $query = ConvertTo-MdbQuery -Types $Types -Where $Where -Select $Select -OrderBy $OrderBy `
                -GroupBy $GroupBy -Summaries $Summaries -Limit $Limit -Offset $Offset -Context $Context `
                -FrontmatterMode $FrontmatterMode -IncludeBody:$IncludeBody
            $compiled = [Mdbase.Core.Query.MdbCompiledQuery]::Compile($query)
            $resultSet = $compiled.Execute($Collection)
        } catch {
            $PSCmdlet.ThrowTerminatingError((ConvertTo-MdbErrorRecord -Exception $_.Exception))
            return
        }

        foreach ($diagnostic in $resultSet.Diagnostics) {
            $location = if ($diagnostic.Path) { " ($($diagnostic.Path))" } else { '' }
            Write-Warning "[$($diagnostic.Code)] $($diagnostic.Message)$location"
        }

        if ($Raw) {
            $resultSet
        } else {
            $resultSet.Results
        }
    }
}
