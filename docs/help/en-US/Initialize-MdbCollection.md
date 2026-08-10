---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Initialize-MdbCollection

## SYNOPSIS
Scaffolds a brand-new mdbase collection and returns a connected handle.

## SYNTAX

```
Initialize-MdbCollection [-Path] <String> [-Force] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION
Writes the smallest valid 'mdbase.yaml' MdbCollectionConfig.Parse accepts plus the
default types/contracts folders, then delegates to Connect-MdbCollection for the
returned handle.
No Mdbase.Core bootstrap primitive exists - Connect requires an
existing config - so this cmdlet owns the scaffold-file-writing itself.

## EXAMPLES

### EXAMPLE 1
```
$c = Initialize-MdbCollection -Path ./new-collection
```

Scaffolds './new-collection/mdbase.yaml' plus '_types/'/'_contracts/', then connects.

## PARAMETERS

### -Path
Directory to initialize.
Created if it does not already exist.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Overwrites an existing 'mdbase.yaml' at Path.
Without it, an existing 'mdbase.yaml'
is a terminating error so Initialize-MdbCollection can never accidentally clobber a
collection meant to be opened with Connect-MdbCollection instead.

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

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
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

### Mdbase.Core.MdbCollection
## NOTES

## RELATED LINKS
