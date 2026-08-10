---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Rename-MdbRecord

## SYNOPSIS
Moves an mdbase record to a new collection-relative path.

## SYNTAX

```
Rename-MdbRecord -Collection <MdbCollection> [-Path] <String> [-NewPath] <String> [-IfRevision <String>]
 [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
A thin wrapper over MdbCollection.Rename.
Does not rewrite link text in any other
record.
-WhatIf runs Mdbase.Core's own dryRun path, so the pipeline still gets the
would-be-renamed MdbRecord as a preview; nothing is persisted.

## EXAMPLES

### EXAMPLE 1
```
Rename-MdbRecord -Collection $c -Path tasks/fix-login.md -NewPath tasks/fix-login-page.md
```

Moves the record to its new path.

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
Collection-relative path of the record to rename.

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

### -NewPath
The destination collection-relative path.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IfRevision
Optimistic-concurrency check against the record's current Revision.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
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

### Mdbase.Core.MdbRecord
## NOTES

## RELATED LINKS
