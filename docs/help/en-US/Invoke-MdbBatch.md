---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Invoke-MdbBatch

## SYNOPSIS
Runs an ordered batch of create/update/delete/rename operations against a collection.

## SYNTAX

```
Invoke-MdbBatch [-Collection] <MdbCollection> [-Operation] <Hashtable[]> [-AllowPartial]
 [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Each input hashtable (Kind plus that kind's fields) maps to one MdbBatchOperation
factory call, then MdbCollection.ExecuteBatch.
Never throws for a per-operation failure
- one PowerShell object per MdbBatchOperationResult (Valid/Path/Result/Diagnostics) is
emitted instead, so scripted bulk changes get the spec's own envelope shape rather than
N separate terminating-error try/catches.

Without -AllowPartial, every operation is validated (each already-validated operation's
effect visible to the next operation's own uniqueness/path checks) before any of them
persists; the whole batch aborts on the first invalid operation.
-AllowPartial instead
validates-and-writes each operation independently, continuing past individual failures.

## EXAMPLES

### EXAMPLE 1
```
Invoke-MdbBatch -Collection $c -Operation @(
        @{ Kind = 'Update'; Path = 'tasks/a.md'; Patch = @{ status = 'closed' } }
        @{ Kind = 'Delete'; Path = 'tasks/b.md' }
    )
```

Closes one task and deletes another as a single validated batch.

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

### -Operation
One hashtable per operation.
Every entry requires 'Kind' (Create/Update/Delete/Rename)
plus that kind's own fields: Create (Frontmatter/Body/Types/Path), Update
(Path/Patch/Remove/Body/Document/IfRevision), Delete (Path/IfRevision), Rename
(Path/NewPath/IfRevision).

```yaml
Type: Hashtable[]
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowPartial
Validates and writes each operation independently, continuing past individual failures,
instead of validating the whole batch before any of it persists.

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

### Mdbase.Core.Write.MdbBatchOperationResult
## NOTES

## RELATED LINKS
