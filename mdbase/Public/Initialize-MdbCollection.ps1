function Initialize-MdbCollection {
    <#
    .SYNOPSIS
        Scaffolds a brand-new mdbase collection and returns a connected handle.
    .DESCRIPTION
        Writes the smallest valid 'mdbase.yaml' MdbCollectionConfig.Parse accepts plus the
        default types/contracts folders, then delegates to Connect-MdbCollection for the
        returned handle. No Mdbase.Core bootstrap primitive exists — Connect requires an
        existing config — so this cmdlet owns the scaffold-file-writing itself.
    .PARAMETER Path
        Directory to initialize. Created if it does not already exist.
    .PARAMETER Force
        Overwrites an existing 'mdbase.yaml' at Path. Without it, an existing 'mdbase.yaml'
        is a terminating error so Initialize-MdbCollection can never accidentally clobber a
        collection meant to be opened with Connect-MdbCollection instead.
    .EXAMPLE
        PS> $c = Initialize-MdbCollection -Path ./new-collection

        Scaffolds './new-collection/mdbase.yaml' plus '_types/'/'_contracts/', then connects.
    .OUTPUTS
        Mdbase.Core.MdbCollection
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
    [OutputType([Mdbase.Core.MdbCollection])]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Path,

        [switch]$Force
    )

    $configPath = Join-Path -Path $Path -ChildPath 'mdbase.yaml'
    if ((Test-Path -LiteralPath $configPath -PathType Leaf) -and -not $Force) {
        $diagnostic = [Mdbase.Core.MdbDiagnostic]@{
            Severity = [Mdbase.Core.MdbSeverity]::Error
            Code     = 'collection_already_initialized'
            Message  = "'$configPath' already declares an mdbase collection; pass -Force to overwrite it."
            Path     = $configPath
        }
        $errorRecord = [System.Management.Automation.ErrorRecord]::new(
            [System.InvalidOperationException]::new($diagnostic.Message),
            $diagnostic.Code,
            [System.Management.Automation.ErrorCategory]::ResourceExists,
            $diagnostic)
        $errorRecord.ErrorDetails = [System.Management.Automation.ErrorDetails]::new($diagnostic.Message)
        $PSCmdlet.ThrowTerminatingError($errorRecord)
        return
    }

    if (-not $PSCmdlet.ShouldProcess($Path, 'Initialize mdbase collection')) {
        return
    }

    New-Item -Path $Path -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path -Path $Path -ChildPath '_types') -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path -Path $Path -ChildPath '_contracts') -ItemType Directory -Force | Out-Null
    Set-Content -LiteralPath $configPath -Value "spec_version: `"0.3`"`n" -NoNewline -Encoding utf8

    Connect-MdbCollection -Path $Path
}
