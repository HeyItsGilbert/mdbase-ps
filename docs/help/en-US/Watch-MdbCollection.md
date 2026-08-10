---
external help file: mdbase-help.xml
Module Name: mdbase
online version:
schema: 2.0.0
---

# Watch-MdbCollection

## SYNOPSIS
Watches a connected mdbase collection for filesystem changes.

## SYNTAX

```
Watch-MdbCollection [-Collection] <MdbCollection> [[-ScriptBlock] <ScriptBlock>]
 [[-DebounceMilliseconds] <Int32>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Translates filesystem change/create/delete/rename events into MdbCollection.Refresh(path)
calls, debounced per-path against editors that write in multiple steps (#42 point 19).

With no -ScriptBlock, this call blocks, emitting each refreshed MdbRecord (or, once a
path no longer exists, its collection-relative path as a plain string) onto the pipeline
as changes settle.
Ctrl+C (or piping into something that stops the pipeline) tears down
the underlying FileSystemWatcher via the loop's own try/finally.

With -ScriptBlock, this call instead registers the same debounced loop on a background
runspace and returns immediately with a stoppable handle - the calling script's own
control flow is never blocked.
Call the handle's Stop() method to tear down the watcher.

## EXAMPLES

### EXAMPLE 1
```
Watch-MdbCollection -Collection $c | Where-Object { $_ -is [Mdbase.Core.MdbRecord] }
```

Blocks, streaming every changed record (filtering out removed-path strings).

### EXAMPLE 2
```
$handle = Watch-MdbCollection -Collection $c -ScriptBlock { param($item) $item | Out-File log.txt -Append }
PS> $handle.Stop()
```

Registers an async handler and stops it later.

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

### -ScriptBlock
Invoked once per settled change (async mode), with the refreshed MdbRecord or removed
path as its single argument, on a background runspace.

```yaml
Type: ScriptBlock
Parameter Sets: (All)
Aliases:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DebounceMilliseconds
How long a path must stay quiet before its change is considered settled.
Default 300ms.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
Default value: 300
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

### Mdbase.Core.MdbRecord, System.String (a removed path), or a stoppable watch handle when -ScriptBlock is used.
## NOTES

## RELATED LINKS
