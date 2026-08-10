function ConvertTo-MdbQuery {
    <#
    .SYNOPSIS
        Builds an Mdbase.Core.Query.MdbQuery from Find-MdbRecord's plain-string parameters.
    .DESCRIPTION
        The shared parameter-parsing helper behind Find-MdbRecord (#42): every MdbQuery
        capability (Types/Where/Select/OrderBy/GroupBy/Summaries/Limit/Offset/Context/
        FrontmatterMode) as PowerShell-native parameter shapes instead of Mdbase.Core types, so a
        caller writes plain CEL-source strings and hashtables/arrays.

        -OrderBy/-GroupBy accept "field[:asc|desc]" strings (default ascending).
        -Select accepts either a plain string array (field-name select, Name == Expression) or a
        hashtable of name -> CEL expression.
        -Summaries accepts a hashtable of result-name -> "field:function" strings.

        A malformed order/group/summary entry throws MdbInvalidQueryException directly, so it
        routes through the same 'invalid_query' error-translation path as a query
        MdbCompiledQuery.Compile itself would reject.
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.Query.MdbQuery])]
    param(
        [string[]]$Types,
        [string]$Where,
        [object]$Select,
        [string[]]$OrderBy,
        [string[]]$GroupBy,
        [hashtable]$Summaries,
        [Nullable[int]]$Limit,
        [Nullable[int]]$Offset,
        [string]$Context,
        [string]$FrontmatterMode = 'Effective',
        [switch]$IncludeBody
    )

    function ConvertTo-SortKey([string]$Entry) {
        $parts = $Entry.Split(':', 2)
        $field = $parts[0]
        $direction = [Mdbase.Core.Query.MdbSortDirection]::Ascending
        if ($parts.Count -eq 2) {
            $direction = switch ($parts[1].Trim().ToLowerInvariant()) {
                'asc' { [Mdbase.Core.Query.MdbSortDirection]::Ascending }
                'desc' { [Mdbase.Core.Query.MdbSortDirection]::Descending }
                default {
                    throw [Mdbase.Core.Query.MdbInvalidQueryException]::new(
                        "Sort entry '$Entry' has an invalid direction '$($parts[1])'; expected 'asc' or 'desc'.")
                }
            }
        }

        [Mdbase.Core.Query.MdbSortKey]::new($field, $direction)
    }

    $selectItems = [System.Collections.Generic.List[Mdbase.Core.Query.MdbSelectItem]]::new()
    if ($Select -is [System.Collections.IDictionary]) {
        foreach ($key in $Select.Keys) {
            $selectItems.Add([Mdbase.Core.Query.MdbSelectItem]::new([string]$key, [string]$Select[$key]))
        }
    } elseif ($Select) {
        foreach ($field in $Select) {
            $selectItems.Add([Mdbase.Core.Query.MdbSelectItem]::new([string]$field, [string]$field))
        }
    }

    $orderByKeys = [System.Collections.Generic.List[Mdbase.Core.Query.MdbSortKey]]::new()
    foreach ($entry in $OrderBy) { $orderByKeys.Add((ConvertTo-SortKey $entry)) }

    $groupByKeys = [System.Collections.Generic.List[Mdbase.Core.Query.MdbSortKey]]::new()
    foreach ($entry in $GroupBy) { $groupByKeys.Add((ConvertTo-SortKey $entry)) }

    $summaryRequests = [System.Collections.Generic.List[Mdbase.Core.Query.MdbSummaryRequest]]::new()
    if ($Summaries) {
        foreach ($resultName in $Summaries.Keys) {
            $spec = [string]$Summaries[$resultName]
            $parts = $spec.Split(':', 2)
            if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
                throw [Mdbase.Core.Query.MdbInvalidQueryException]::new(
                    "Summary '$resultName' value '$spec' must be 'field:function'.")
            }

            $summaryRequests.Add([Mdbase.Core.Query.MdbSummaryRequest]::new($parts[0], $parts[1], [string]$resultName))
        }
    }

    $frontmatterModeValue = [Mdbase.Core.Query.MdbFrontmatterMode]$FrontmatterMode

    # Built via ::new() plus direct property assignment, not [Type]@{...} hashtable
    # conversion: PowerShell's hashtable-to-object conversion coerces a $null value assigned to
    # a nullable string property into "" (LanguagePrimitives' $null-to-string behavior), which
    # would turn an omitted -Where/-Context into a live-but-empty CEL predicate/context path
    # instead of the "no predicate"/"context unbound" MdbQuery already treats null as.
    $query = [Mdbase.Core.Query.MdbQuery]::new()
    $query.Types = $Types
    if ($Context) { $query.ContextPath = $Context }
    if ($Where) { $query.Where = $Where }
    $query.Select = $selectItems.AsReadOnly()
    $query.OrderBy = $orderByKeys.AsReadOnly()
    $query.GroupBy = $groupByKeys.AsReadOnly()
    $query.Summaries = $summaryRequests.AsReadOnly()
    $query.Limit = $Limit
    $query.Offset = $Offset
    $query.IncludeBody = [bool]$IncludeBody
    $query.FrontmatterMode = $frontmatterModeValue
    $query
}
