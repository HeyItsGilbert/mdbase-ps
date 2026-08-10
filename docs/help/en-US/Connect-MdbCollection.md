---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Connect-MdbCollection

## SYNOPSIS
Connects to an existing mdbase collection.

## SYNTAX

```
Connect-MdbCollection [-Path] <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Runs Mdbase.Core's full three-phase load (contracts, types, records+links) and returns a
connected handle every other mdbase cmdlet accepts via -Collection - there is no ambient
or global collection state (spec ADR-0002).

## EXAMPLES

### EXAMPLE 1
```
$c = Connect-MdbCollection -Path ./my-collection
```

Connects to the collection rooted at './my-collection'.

## PARAMETERS

### -Path
Directory containing the collection's 'mdbase.yaml'.

```yaml
Type: String
Parameter Sets: (All)
Aliases: FullName

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
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

### Mdbase.Core.MdbCollection
## NOTES

## RELATED LINKS
