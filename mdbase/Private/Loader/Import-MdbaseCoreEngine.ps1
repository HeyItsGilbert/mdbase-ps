using namespace System.Reflection
using namespace System.Runtime.Loader

# A private AssemblyLoadContext scoped to this module (#29, ADR-0002 "Assembly loading") for
# Mdbase.Core.dll's transitive dependencies (JsonSchema.Net, Celly, YamlDotNet, Markdig,
# Org.Webpki.JsonCanonicalizer) — resolved through ordinary same-folder AssemblyDependencyResolver
# lookups against `lib/net8.0/`, no per-assembly registration list to maintain. Isolating those
# dependencies means mdbase-ps never collides with another module (or the user's own script)
# loading a different version of the same library.
#
# Mdbase.Core.dll itself loads into the *default* AssemblyLoadContext rather than this one: the
# PowerShell engine's own `[TypeName]` resolver (used by every cmdlet's typed parameters, e.g.
# `[Mdbase.Core.MdbCollection]$Collection`) only searches assemblies loaded into
# [AssemblyLoadContext]::Default — a type loaded into a *custom* ALC is reachable by reflection
# but not by `[TypeName]` literal syntax. `Mdbase.Core` is a first-party, uniquely-named
# assembly (unlike its vendored third-party dependencies), so the collision risk isolation
# actually guards against does not apply to it.
class MdbaseCoreDependencyLoadContext : System.Runtime.Loader.AssemblyLoadContext {
    hidden [System.Runtime.Loader.AssemblyDependencyResolver] $Resolver

    MdbaseCoreDependencyLoadContext([string]$mainAssemblyPath) : base('mdbase-core-dependencies', $false) {
        $this.Resolver = [System.Runtime.Loader.AssemblyDependencyResolver]::new($mainAssemblyPath)
    }

    [System.Reflection.Assembly] Load([System.Reflection.AssemblyName]$assemblyName) {
        $resolvedPath = $this.Resolver.ResolveAssemblyToPath($assemblyName)
        if ($null -ne $resolvedPath) {
            return $this.LoadFromAssemblyPath($resolvedPath)
        }

        return $null
    }
}

# Import-MdbaseCoreEngine must run at most once per *process*, not once per module instance:
# every test file (and any other caller) does Import-Module/Remove-Module in the same PowerShell
# session, and Remove-Module tears down the module's own session state — so a `$script:` guard
# resets on every reimport and would re-subscribe a Resolving handler each time, accumulating
# stale duplicate delegates on the process-global `[AssemblyLoadContext]::Default` event (each
# closing over an increasingly orphaned dependency context — this previously surfaced as
# "Could not load file or assembly 'Microsoft.PowerShell.CrossCompatibility'" when PSScriptAnalyzer
# ran after 18 Pester files had each imported and removed this module). `[AssemblyLoadContext]::All`
# is authoritative process state instead: once created with `isCollectible: $false`, our named
# context stays rooted there (and its Resolving-handler delegate stays rooted by the Default ALC's
# own event invocation list) for the life of the process, so checking for it here is both the
# correctness guard and sufficient to keep everything alive — no extra script/global state needed.
function Import-MdbaseCoreEngine {
    <#
    .SYNOPSIS
        Loads Mdbase.Core.dll and isolates its transitive dependencies, once per process.
    .DESCRIPTION
        Runs on every module import, in mdbase.psm1, before any Public/Private function is
        dot-sourced, so every cmdlet's parameter type constraints (e.g. [Mdbase.Core.MdbCollection])
        resolve at parse time. Mdbase.Core.dll loads into the default AssemblyLoadContext directly
        (so PowerShell's `[TypeName]` resolver can see it); a private AssemblyLoadContext resolves
        everything it depends on (JsonSchema.Net, Celly, YamlDotNet, Markdig, the RFC 8785
        canonicalizer) via a [AssemblyLoadContext]::Default.Resolving hook, so those stay isolated
        from whatever else is loaded in the host process. A second call in the same process — e.g.
        after Remove-Module/Import-Module — is a no-op.
    .PARAMETER ModuleRoot
        The module's own root directory ($PSScriptRoot from mdbase.psm1); `lib/net8.0/Mdbase.Core.dll`
        is resolved relative to it.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [string]$ModuleRoot
    )

    $alreadyLoaded = [System.Runtime.Loader.AssemblyLoadContext]::All |
        Where-Object { $_.Name -eq 'mdbase-core-dependencies' }
    if ($null -ne $alreadyLoaded) {
        return
    }

    $assemblyPath = Join-Path -Path $ModuleRoot -ChildPath 'lib/net8.0/Mdbase.Core.dll'
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Mdbase.Core.dll was not found at '$assemblyPath'. Build the module first (psake 'Build' task publishes the Core Engine into lib/net8.0/)."
    }

    $dependencyLoadContext = [MdbaseCoreDependencyLoadContext]::new($assemblyPath)

    $resolvingHandler = [System.Func[System.Runtime.Loader.AssemblyLoadContext, System.Reflection.AssemblyName, System.Reflection.Assembly]] {
        # $context (the requesting AssemblyLoadContext) is part of the Resolving event's fixed
        # delegate signature and unused here by design.
        param($context, $assemblyName)
        if ($assemblyName.Name -eq 'Mdbase.Core') {
            return $null
        }

        # Calling .Load() directly (the resolver-backed override above) rather than
        # .LoadFromAssemblyName(): the latter, on an unresolved name, falls back to the full
        # assembly-binding ceremony and re-raises Default.Resolving for the very same name —
        # infinite recursion (observed as a stack overflow) for any assembly this resolver
        # can't satisfy, e.g. one probed by unrelated in-process tooling like PSScriptAnalyzer.
        return $dependencyLoadContext.Load($assemblyName)
    }.GetNewClosure()
    [System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolvingHandler)

    [System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($assemblyPath) | Out-Null
}
