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

# The loaded MdbaseCoreDependencyLoadContext and its Default.Resolving handler delegate, kept
# alive for the module's lifetime via script scope — a delegate reachable only through a
# function-local variable is not a reliable GC root once that function returns. Populated once
# by Import-MdbaseCoreEngine; a second call is a no-op so re-dot-sourcing this file (e.g. under
# Pester `InModuleScope`) never double-loads the assembly or double-subscribes Resolving.
$script:MdbaseCoreDependencyLoadContext = $null
$script:MdbaseCoreResolvingHandler = $null

function Import-MdbaseCoreEngine {
    <#
    .SYNOPSIS
        Loads Mdbase.Core.dll and isolates its transitive dependencies, once per session.
    .DESCRIPTION
        Runs once, in mdbase.psm1, before any Public/Private function is dot-sourced, so every
        cmdlet's parameter type constraints (e.g. [Mdbase.Core.MdbCollection]) resolve at parse
        time. Mdbase.Core.dll loads into the default AssemblyLoadContext directly (so PowerShell's
        `[TypeName]` resolver can see it); a private AssemblyLoadContext resolves everything it
        depends on (JsonSchema.Net, Celly, YamlDotNet, Markdig, the RFC 8785 canonicalizer)
        via a [AssemblyLoadContext]::Default.Resolving hook, so those stay isolated from whatever
        else is loaded in the host process.
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

    if ($null -ne $script:MdbaseCoreDependencyLoadContext) {
        return
    }

    $assemblyPath = Join-Path -Path $ModuleRoot -ChildPath 'lib/net8.0/Mdbase.Core.dll'
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw "Mdbase.Core.dll was not found at '$assemblyPath'. Build the module first (psake 'Build' task publishes the Core Engine into lib/net8.0/)."
    }

    $script:MdbaseCoreDependencyLoadContext = [MdbaseCoreDependencyLoadContext]::new($assemblyPath)

    $script:MdbaseCoreResolvingHandler = [System.Func[System.Runtime.Loader.AssemblyLoadContext, System.Reflection.AssemblyName, System.Reflection.Assembly]] {
        # $context (the requesting AssemblyLoadContext) is part of the Resolving event's fixed
        # delegate signature and unused here by design.
        param($context, $assemblyName)
        if ($assemblyName.Name -eq 'Mdbase.Core') {
            return $null
        }

        return $script:MdbaseCoreDependencyLoadContext.LoadFromAssemblyName($assemblyName)
    }
    [System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($script:MdbaseCoreResolvingHandler)

    [System.Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($assemblyPath) | Out-Null
}
