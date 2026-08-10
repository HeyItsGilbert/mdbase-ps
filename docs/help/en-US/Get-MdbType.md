---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Get-MdbType

## SYNOPSIS
Gets one or every loaded type from a connected mdbase collection's type registry.

## SYNTAX

```
Get-MdbType -Collection <MdbCollection> [[-Name] <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
-Name is identity-based lookup (case-insensitive, matching Mdbase.Core's own canonical
name comparison); without it, every loaded MdbType is emitted onto the pipeline.

## EXAMPLES

### EXAMPLE 1
```
Get-MdbType -Collection $c -Name Task
```

Returns the loaded 'Task' type.

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

### -Name
The type name to fetch, compared case-insensitively.

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

### Mdbase.Core.MdbType
## NOTES

## RELATED LINKS
