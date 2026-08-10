function Watch-MdbCollection {
    <#
    .SYNOPSIS
        Watches a connected mdbase collection for filesystem changes.
    .DESCRIPTION
        Translates filesystem change/create/delete/rename events into MdbCollection.Refresh(path)
        calls, debounced per-path against editors that write in multiple steps (#42 point 19).

        With no -ScriptBlock, this call blocks, emitting each refreshed MdbRecord (or, once a
        path no longer exists, its collection-relative path as a plain string) onto the pipeline
        as changes settle. Ctrl+C (or piping into something that stops the pipeline) tears down
        the underlying FileSystemWatcher via the loop's own try/finally.

        With -ScriptBlock, this call instead registers the same debounced loop on a background
        runspace and returns immediately with a stoppable handle — the calling script's own
        control flow is never blocked. Call the handle's Stop() method to tear down the watcher.
    .PARAMETER Collection
        A handle returned by Connect-MdbCollection/Initialize-MdbCollection.
    .PARAMETER ScriptBlock
        Invoked once per settled change (async mode), with the refreshed MdbRecord or removed
        path as its single argument, on a background runspace.
    .PARAMETER DebounceMilliseconds
        How long a path must stay quiet before its change is considered settled. Default 300ms.
    .EXAMPLE
        PS> Watch-MdbCollection -Collection $c | Where-Object { $_ -is [Mdbase.Core.MdbRecord] }

        Blocks, streaming every changed record (filtering out removed-path strings).
    .EXAMPLE
        PS> $handle = Watch-MdbCollection -Collection $c -ScriptBlock { param($item) $item | Out-File log.txt -Append }
        PS> $handle.Stop()

        Registers an async handler and stops it later.
    .OUTPUTS
        Mdbase.Core.MdbRecord, System.String (a removed path), or a stoppable watch handle when -ScriptBlock is used.
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.MdbRecord])]
    [OutputType([string])]
    [OutputType('Mdbase.WatchHandle')]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Mdbase.Core.MdbCollection]$Collection,

        [scriptblock]$ScriptBlock,

        [int]$DebounceMilliseconds = 300
    )

    process {
        if ($PSBoundParameters.ContainsKey('ScriptBlock')) {
            # A module-private scriptblock (Get-MdbCollectionWatchLoop's result) is bound to the
            # runspace its module was imported into — invoking it after handing it to a *different*
            # runspace via SessionStateProxy silently no-ops (verified empirically). The reliable
            # cross-runspace primitives are: import the module fresh in the background runspace,
            # then drive its own public (blocking-mode) Watch-MdbCollection pipeline there.
            $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath 'mdbase.psd1'
            $runspace = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
            $runspace.Open()
            $runspace.SessionStateProxy.SetVariable('Collection', $Collection)
            $runspace.SessionStateProxy.SetVariable('UserScriptBlock', $ScriptBlock)
            $runspace.SessionStateProxy.SetVariable('DebounceMilliseconds', $DebounceMilliseconds)
            $runspace.SessionStateProxy.SetVariable('ModulePath', $modulePath)

            $powershell = [powershell]::Create()
            $powershell.Runspace = $runspace
            $null = $powershell.AddScript({
                    Import-Module -Name $ModulePath -Force
                    Watch-MdbCollection -Collection $Collection -DebounceMilliseconds $DebounceMilliseconds |
                        ForEach-Object { & $UserScriptBlock $_ }
                })

            $asyncResult = $powershell.BeginInvoke()

            $handle = [pscustomobject]@{
                PSTypeName  = 'Mdbase.WatchHandle'
                Collection  = $Collection
                PowerShell  = $powershell
                Runspace    = $runspace
                AsyncResult = $asyncResult
            }
            Add-Member -InputObject $handle -MemberType ScriptMethod -Name Stop -Value {
                $this.PowerShell.Stop()
                $this.PowerShell.Dispose()
                $this.Runspace.Close()
                $this.Runspace.Dispose()
            }

            $handle
            return
        }

        $loop = Get-MdbCollectionWatchLoop
        & $loop -Collection $Collection -Emit { param($item) $item } -DebounceMilliseconds $DebounceMilliseconds
    }
}
