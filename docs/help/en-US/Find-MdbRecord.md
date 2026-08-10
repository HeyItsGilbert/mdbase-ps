---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Find-MdbRecord

## SYNOPSIS
Runs an MdbQuery against a connected mdbase collection.

## SYNTAX

```
Find-MdbRecord [-Collection] <MdbCollection> [[-Types] <String[]>] [[-Where] <String>] [[-Select] <Object>]
 [[-OrderBy] <String[]>] [[-GroupBy] <String[]>] [[-Summaries] <Hashtable>] [[-Limit] <Int32>]
 [[-Offset] <Int32>] [[-Context] <String>] [[-FrontmatterMode] <String>] [-IncludeBody] [-Raw]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Exposes every MdbQuery capability as PowerShell parameters accepting plain strings for
CEL expressions.
Emits the query's Results by default; -Raw instead returns the full
MdbQueryResultSet (including Meta/Diagnostics) for grouped/summarized queries.
Each
result-set diagnostic (a where/projection/summary evaluation error on one record) is
written to the warning stream rather than suppressing the rest of the results.

## EXAMPLES

### EXAMPLE 1
```
Find-MdbRecord -Collection $c -Where 'status == "open"'
```

Every record whose effective 'status' field is "open".

## PARAMETERS

### -Collection
A handle returned by Connect-MdbCollection/Initialize-MdbCollection.

```yaml
Type: MdbCollection
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Types
OR-filter by matched-type membership; every record is a candidate when omitted.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Where
A CEL predicate source string; every candidate passes when omitted.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Select
Either a string array of field names, or a hashtable of name -\> CEL expression.

```yaml
Type: Object
Parameter Sets: (All)
Aliases:

Required: False
Position: 4
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OrderBy
Field references as "field" or "field:asc"/"field:desc" (default ascending).

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 5
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GroupBy
Field references, same "field\[:asc|desc\]" shape as -OrderBy.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 6
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Summaries
A hashtable of result-name -\> "field:function".

```yaml
Type: Hashtable
Parameter Sets: (All)
Aliases:

Required: False
Position: 7
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Limit
{{ Fill Limit Description }}

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: 8
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Offset
{{ Fill Offset Description }}

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: 9
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Context
Collection-relative path resolved once into \`context.this\`.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 10
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FrontmatterMode
Which frontmatter member(s) serialize into each result: Effective (default), Persisted, or Both.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 11
Default value: Effective
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeBody
{{ Fill IncludeBody Description }}

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Raw
Emits the full MdbQueryResultSet (Results/Meta/Diagnostics) instead of just Results.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
{{ Fill ProgressAction Description }}

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Mdbase.Core.Query.MdbQueryResult, or Mdbase.Core.Query.MdbQueryResultSet with -Raw.
## NOTES

## RELATED LINKS
