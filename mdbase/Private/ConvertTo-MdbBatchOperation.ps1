function ConvertTo-MdbBatchOperationInternal {
    <#
    .SYNOPSIS
        Maps one Invoke-MdbBatch operation descriptor hashtable to an MdbBatchOperation.
    #>
    [CmdletBinding()]
    [OutputType([Mdbase.Core.Write.MdbBatchOperation])]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Operation
    )

    if (-not $Operation.Contains('Kind') -or [string]::IsNullOrWhiteSpace([string]$Operation['Kind'])) {
        throw [System.ArgumentException]::new("Every batch operation descriptor requires a 'Kind' (Create, Update, Delete, or Rename).")
    }

    $frontmatter = if ($Operation.Contains('Frontmatter') -and $null -ne $Operation['Frontmatter']) {
        ConvertTo-MdbFrontmatter -Frontmatter $Operation['Frontmatter']
    } else {
        $null
    }
    $patch = if ($Operation.Contains('Patch') -and $null -ne $Operation['Patch']) {
        ConvertTo-MdbFrontmatter -Frontmatter $Operation['Patch']
    } else {
        $null
    }

    # PowerShell's dynamic .NET method binder coerces an explicit $null argument bound to a
    # `string` parameter back into "" (verified empirically) — routing a genuine null (a
    # hashtable key the caller simply omitted) through to Mdbase.Core requires
    # [NullString]::Value, assigned directly into a plain variable per field (a value returned
    # *through* a function call loses the NullString sentinel, so this cannot be a helper call).
    $bodyValue = $Operation['Body']
    $bodyArg = if ($null -eq $bodyValue) { [NullString]::Value } else { $bodyValue }
    $pathValue = $Operation['Path']
    $pathArg = if ($null -eq $pathValue) { [NullString]::Value } else { $pathValue }
    $documentValue = $Operation['Document']
    $documentArg = if ($null -eq $documentValue) { [NullString]::Value } else { $documentValue }
    $ifRevisionValue = $Operation['IfRevision']
    $ifRevisionArg = if ($null -eq $ifRevisionValue) { [NullString]::Value } else { $ifRevisionValue }

    switch (([string]$Operation['Kind']).ToLowerInvariant()) {
        'create' {
            [Mdbase.Core.Write.MdbBatchOperation]::Create($frontmatter, $bodyArg, $Operation['Types'], $pathArg)
        }
        'update' {
            [Mdbase.Core.Write.MdbBatchOperation]::Update($Operation['Path'], $patch, $Operation['Remove'], $bodyArg, $documentArg, $ifRevisionArg)
        }
        'delete' {
            [Mdbase.Core.Write.MdbBatchOperation]::Delete($Operation['Path'], $ifRevisionArg)
        }
        'rename' {
            [Mdbase.Core.Write.MdbBatchOperation]::Rename($Operation['Path'], $Operation['NewPath'], $ifRevisionArg)
        }
        default {
            throw [System.ArgumentException]::new("Unknown batch operation Kind '$($Operation['Kind'])'; expected Create, Update, Delete, or Rename.")
        }
    }
}
