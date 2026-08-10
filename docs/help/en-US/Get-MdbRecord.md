---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Get-MdbRecord

## SYNOPSIS
Gets one or every loaded record from a connected mdbase collection.

## SYNTAX

```
Get-MdbRecord -Collection <MdbCollection> [[-Path] <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
-Path is identity-based lookup: the exact loaded MdbRecord for that path, or a
terminating 'record_not_found' error.
Without -Path, every indexed record is emitted
onto the pipeline one at a time - enumerating everything is not a separate cmdlet.

## EXAMPLES

### EXAMPLE 1
```
Get-MdbRecord -Collection $c -Path tasks/fix-login.md
```

Returns the loaded record at that path.

### EXAMPLE 2
```
Get-MdbRecord -Collection $c
```

Emits every loaded record.

## PARAMETERS

### -Collection
A handle returned by Connect-MdbCollection/Initialize-MdbCollection.

```yaml
Type: MdbCollection
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Collection-relative path of the record to fetch.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: None
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

### Mdbase.Core.MdbRecord
## NOTES

## RELATED LINKS
