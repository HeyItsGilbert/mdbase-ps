# Load the Core Engine (Mdbase.Core.dll) into its own AssemblyLoadContext before anything
# else in this module is dot-sourced, so Public/Private parameter type constraints
# (e.g. [Mdbase.Core.MdbCollection]) resolve at parse time (#29, ADR-0002).
. (Join-Path -Path $PSScriptRoot -ChildPath 'Private/Loader/Import-MdbaseCoreEngine.ps1')
Import-MdbaseCoreEngine -ModuleRoot $PSScriptRoot

# Dot source public/private functions. Private/Loader is excluded here — it is already
# dot-sourced above, and doing so again would redefine the MdbaseCoreLoadContext class.
# Prefix (not exact-equality) match: -Recurse means any future nested folder under
# Private/Loader must be excluded too, not just files directly inside it.
$loaderPath = Join-Path -Path $PSScriptRoot -ChildPath 'Private/Loader'
$public  = @(Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Public/*.ps1')  -Recurse -ErrorAction Stop)
$private = @(Get-ChildItem -Path (Join-Path -Path $PSScriptRoot -ChildPath 'Private/*.ps1') -Recurse -ErrorAction Stop |
    Where-Object { $_.DirectoryName -ne $loaderPath -and -not $_.DirectoryName.StartsWith($loaderPath + [IO.Path]::DirectorySeparatorChar) })
foreach ($import in @($private + $public)) {
    try {
        . $import.FullName
    } catch {
        throw "Unable to dot source [$($import.FullName)]"
    }
}

Export-ModuleMember -Function $public.Basename
