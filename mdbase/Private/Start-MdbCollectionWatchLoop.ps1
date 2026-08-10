# The debounced FileSystemWatcher loop shared by Watch-MdbCollection's blocking and async
# modes (#42 point 19): filesystem change/create/delete/rename events translate into
# MdbCollection.Refresh(path) calls, coalesced per-path against editors that write in
# multiple steps. Kept as a single scriptblock (rather than a private function) so the exact
# same logic runs both directly in the calling pipeline (blocking mode) and inside a
# background runspace via PowerShell.AddScript (async mode) without re-resolving a
# module-private function name across the runspace boundary.
$script:MdbWatchLoopScript = {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [Mdbase.Core.MdbCollection]$Collection,

        # Invoked once per settled change with either the refreshed MdbRecord or, when the path
        # no longer exists, its collection-relative path (a plain string).
        [Parameter(Mandatory)]
        [scriptblock]$Emit,

        [int]$DebounceMilliseconds = 300
    )

    $excludedRoots = '.git', 'node_modules', '.mdbase'
    $watcher = [System.IO.FileSystemWatcher]::new($Collection.RootPath)
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [System.IO.NotifyFilters]'FileName, DirectoryName, LastWrite, Size'

    $pending = [System.Collections.Generic.Dictionary[string, datetime]]::new()
    $subscriptions = [System.Collections.Generic.List[string]]::new()

    try {
        foreach ($eventName in 'Changed', 'Created', 'Deleted', 'Renamed') {
            $subscriptionId = "MdbWatch.$eventName.$([guid]::NewGuid())"
            Register-ObjectEvent -InputObject $watcher -EventName $eventName -SourceIdentifier $subscriptionId -MessageData $pending -Action {
                $changeArgs = $Event.SourceEventArgs
                $map = $Event.MessageData
                $map[$changeArgs.Name] = [datetime]::UtcNow
                if ($changeArgs -is [System.IO.RenamedEventArgs]) {
                    $map[$changeArgs.OldName] = [datetime]::UtcNow
                }
            } | Out-Null
            $subscriptions.Add($subscriptionId)
        }

        $watcher.EnableRaisingEvents = $true
        $debounce = [timespan]::FromMilliseconds($DebounceMilliseconds)

        while ($true) {
            Wait-Event -Timeout 0.2 | Remove-Event

            $now = [datetime]::UtcNow
            $settled = @($pending.Keys | Where-Object { ($now - $pending[$_]) -ge $debounce })
            foreach ($name in $settled) {
                $pending.Remove($name) | Out-Null
                $relativePath = $name.Replace('\', '/')
                $isExcluded = $false
                foreach ($root in $excludedRoots) {
                    if ($relativePath -eq $root -or $relativePath.StartsWith("$root/", [System.StringComparison]::Ordinal)) {
                        $isExcluded = $true
                        break
                    }
                }

                if ($isExcluded) {
                    continue
                }

                $Collection.Refresh($relativePath)
                $record = $null
                if ($Collection.Records.TryGetValue($relativePath, [ref]$record)) {
                    & $Emit $record
                } else {
                    & $Emit $relativePath
                }
            }
        }
    } finally {
        $watcher.EnableRaisingEvents = $false
        foreach ($subscriptionId in $subscriptions) {
            Unregister-Event -SourceIdentifier $subscriptionId -ErrorAction SilentlyContinue
            Get-Job -Name $subscriptionId -ErrorAction SilentlyContinue | Remove-Job -Force -ErrorAction SilentlyContinue
        }

        $watcher.Dispose()
    }
}

function Get-MdbCollectionWatchLoop {
    <#
    .SYNOPSIS
        Returns the shared Watch-MdbCollection debounce loop scriptblock.
    #>
    [CmdletBinding()]
    [OutputType([scriptblock])]
    param()

    $script:MdbWatchLoopScript
}
