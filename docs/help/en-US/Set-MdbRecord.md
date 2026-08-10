---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Set-MdbRecord

## SYNOPSIS
Modifies an existing mdbase record.

## SYNTAX

### Patch (Default)
```
Set-MdbRecord -Collection <MdbCollection> -Path <String> [-Patch <IDictionary>] [-Remove <String[]>]
 [-Body <String>] [-IfRevision <String>] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### Document
```
Set-MdbRecord -Collection <MdbCollection> -Path <String> -Document <String> [-IfRevision <String>]
 [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Two mutually exclusive parameter sets map directly onto MdbCollection.Update's own
patch/document distinction: PowerShell's parameter-set validation enforces the mutual
exclusivity Mdbase.Core already enforces internally, instead of a manual runtime check.
-WhatIf runs Mdbase.Core's own dryRun path, so the pipeline still gets the would-be
MdbRecord as a preview; nothing is persisted.

## EXAMPLES

### EXAMPLE 1
```
Set-MdbRecord -Collection $c -Path tasks/fix-login.md -Patch @{ status = 'closed' }
```

Sets 'status' to "closed", leaving every other field untouched.

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
Collection-relative path of the record to update.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Patch
A set/null patch applied to only the present keys.

```yaml
Type: IDictionary
Parameter Sets: Patch
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Remove
Keys to delete outright, applied alongside -Patch.

```yaml
Type: String[]
Parameter Sets: Patch
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Body
A body override, applied alongside -Patch.

```yaml
Type: String
Parameter Sets: Patch
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Document
A complete replacement Markdown source; mutually exclusive with -Patch/-Remove/-Body.

```yaml
Type: String
Parameter Sets: Document
Aliases:

Required: True
Position: Named
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
