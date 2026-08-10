#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.4.0' }

BeforeAll {
    . (Join-Path $PSScriptRoot 'Common.ps1')
    Import-MdbaseModuleUnderTest
    $script:modulePath = (Resolve-Path (Join-Path $PSScriptRoot '../../mdbase/mdbase.psd1')).Path
}

Describe 'Watch-MdbCollection' {
    It 'blocking mode emits a refreshed MdbRecord for a real filesystem change, within a bounded timeout' {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection-blocking')
        $collection = Connect-MdbCollection -Path $root

        # Blocking mode never returns on its own; drive it on a manually-owned background
        # runspace/PSDataCollection with a hard timeout instead of an indefinite Wait-Job, per
        # #42's testing decisions.
        $runspace = [runspacefactory]::CreateRunspace()
        $runspace.Open()
        $runspace.SessionStateProxy.SetVariable('Collection', $collection)
        $runspace.SessionStateProxy.SetVariable('ModulePath', $script:modulePath)
        $ps = [powershell]::Create()
        $ps.Runspace = $runspace
        $null = $ps.AddScript({
                Import-Module -Name $ModulePath -Force
                Watch-MdbCollection -Collection $Collection -DebounceMilliseconds 50
            })
        $inputBuffer = [System.Management.Automation.PSDataCollection[psobject]]::new()
        $inputBuffer.Complete()
        $outputBuffer = [System.Management.Automation.PSDataCollection[psobject]]::new()
        $null = $ps.BeginInvoke($inputBuffer, $outputBuffer)

        try {
            # A short startup delay: the background runspace still needs to import the module and
            # enable the FileSystemWatcher before any change is observable — creating the record
            # immediately after BeginInvoke() risks a race where the watcher isn't listening yet.
            Start-Sleep -Milliseconds 500
            New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Watched' } -Path 'watched.md' -Confirm:$false | Out-Null

            $deadline = (Get-Date).AddSeconds(10)
            $sawRecord = $false
            while ((Get-Date) -lt $deadline -and -not $sawRecord) {
                Start-Sleep -Milliseconds 200
                # Index-based access, not `$outputBuffer | ...`: piping an still-open
                # PSDataCollection enumerates it with blocking-collection semantics (it waits for
                # more items rather than returning what's there), which would hang this loop
                # until Stop() — defeating the bounded-timeout point of polling in the first place.
                for ($i = 0; $i -lt $outputBuffer.Count; $i++) {
                    $value = $outputBuffer[$i]
                    if ($value -is [Mdbase.Core.MdbRecord] -and $value.FileInfo.Path -eq 'watched.md') {
                        $sawRecord = $true
                    }
                }
            }

            $sawRecord | Should -BeTrue
        } finally {
            $ps.Stop()
            $ps.Dispose()
            $runspace.Close()
            $runspace.Dispose()
        }
    }

    It 'async -ScriptBlock mode returns immediately with a stoppable handle and invokes the handler, within a bounded timeout' {
        $root = New-MdbFixtureCollection -Root (Join-Path $TestDrive 'collection-async')
        $collection = Connect-MdbCollection -Path $root
        $logFile = Join-Path $TestDrive 'async-watch.log'

        $handler = { param($item) Add-Content -Path $logFile -Value 'settled' }.GetNewClosure()

        $before = Get-Date
        $handle = Watch-MdbCollection -Collection $collection -ScriptBlock $handler -DebounceMilliseconds 50
        $elapsed = (Get-Date) - $before

        try {
            $elapsed.TotalSeconds | Should -BeLessThan 5
            $handle.PSObject.TypeNames | Should -Contain 'Mdbase.WatchHandle'
            $handle.PSObject.Methods.Name | Should -Contain 'Stop'

            # Matches the blocking-mode test's startup delay: the handle's own background
            # runspace still needs to import the module and enable the FileSystemWatcher before
            # any change is observable.
            Start-Sleep -Milliseconds 500
            New-MdbRecord -Collection $collection -Frontmatter @{ title = 'Async' } -Path 'async.md' -Confirm:$false | Out-Null

            $deadline = (Get-Date).AddSeconds(10)
            while ((Get-Date) -lt $deadline -and -not (Test-Path $logFile)) {
                Start-Sleep -Milliseconds 200
            }

            Test-Path $logFile | Should -BeTrue
        } finally {
            $handle.Stop()
        }
    }
}
