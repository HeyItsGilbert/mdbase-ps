properties {
    # Set this to $true to create a module with a monolithic PSM1
    $PSBPreference.Build.CompileModule = $false
    $PSBPreference.Help.DefaultLocale = 'en-US'
    $PSBPreference.Test.OutputFile = 'out/testResults.xml'
}

task Default -depends Test

# Publishes the Core Engine (Mdbase.Core.dll + transitive dependencies: JsonSchema.Net, Celly,
# YamlDotNet, Markdig, Org.Webpki.JsonCanonicalizer) into mdbase/lib/net8.0/, where the module's
# private AssemblyLoadContext loader (#29) expects to find it. Declared as a StageFiles PreAction
# so the published DLL exists before Build-PSBuildModule copies the module source tree to Output/.
task PublishCoreEngine {
    $publishOutDir = Join-Path -Path $PSBPreference.General.SrcRootDir -ChildPath 'lib/net8.0'
    exec {
        dotnet publish (Join-Path -Path $PSBPreference.General.ProjectRoot -ChildPath 'src/Mdbase.Core') `
            --framework net8.0 --no-self-contained --output $publishOutDir
    }
} -description 'Publishes Mdbase.Core.dll and its dependencies into mdbase/lib/net8.0'

# The single '-FromModule' call that both loads every PowerShellBuild task (Init, Clean,
# StageFiles, Build, Analyze, Pester, Test, Publish, ...) and merges PreAction/PostAction into
# StageFiles specifically — psake's reference-task merge only fires when a shared-task
# declaration and its extra data (Pre/PostAction here) arrive together in one 'Task' call; a
# separate plain 'task StageFiles { ... }' declared beforehand would just be a second, ordinary
# definition of the same task name and fail with 'already defined'.
task StageFiles -FromModule PowerShellBuild -minimumVersion '0.6.1' -PreAction {
    Invoke-Task PublishCoreEngine
} -PostAction {
    # Mdbase.Core.dll's transitive-dependency set is fixed by its own package references, not
    # something worth hand-maintaining in the source manifest (#42's ADR-0002 vendoring
    # revisit) — compute FileList against the just-staged Output copy instead.
    $outputManifest = Join-Path -Path $PSBPreference.Build.ModuleOutDir -ChildPath "$($PSBPreference.General.ModuleName).psd1"
    $libDir = Join-Path -Path $PSBPreference.Build.ModuleOutDir -ChildPath 'lib/net8.0'
    $relativeFiles = Get-ChildItem -Path $libDir -File | ForEach-Object {
        ($_.FullName.Substring($PSBPreference.Build.ModuleOutDir.Length + 1)).Replace('\', '/')
    }
    BuildHelpers\Update-Metadata -Path $outputManifest -PropertyName FileList -Value $relativeFiles
}
