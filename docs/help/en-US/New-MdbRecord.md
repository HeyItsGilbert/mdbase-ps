---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# New-MdbRecord

## SYNOPSIS
Creates a new mdbase record.

## SYNTAX

```
New-MdbRecord [-Collection] <MdbCollection> [-Frontmatter] <IDictionary> [[-Body] <String>]
 [[-Types] <String[]>] [[-Path] <String>] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION
A thin wrapper over MdbCollection.Create.
-WhatIf runs Mdbase.Core's own dryRun path -
Core's validation/lifecycle/path-generation all still run, so the pipeline still gets the
would-be MdbRecord as a preview, alongside PowerShell's usual "What if:" message; nothing
is persisted.
Every hard failure (schema, path_conflict, unique_conflict, etc.) is a
terminating error carrying the engine's diagnostic - ordinary try/catch, no envelope to parse.

## EXAMPLES

### EXAMPLE 1
```
New-MdbRecord -Collection $c -Frontmatter @{ title = 'Fix login'; status = 'open' } -Types task
```

Creates a new record matched to the 'task' type.

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

### -Frontmatter
The record's input frontmatter mapping.

```yaml
Type: IDictionary
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Body
The record's Markdown body, after the frontmatter block.

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

### -Types
Explicit type membership, taking precedence over the frontmatter's own explicit type
key(s) and inferred matching.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 4
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
An explicit target path; omitted, it is generated from the matched types'
collection.path.pattern.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 5
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
