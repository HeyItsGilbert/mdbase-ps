function ConvertTo-MdbFrontmatterValue {
    <#
    .SYNOPSIS
        Recursively coerces one PowerShell value into mdbase's in-memory JSON data-model shape.
    .DESCRIPTION
        Mdbase.Core.Json.JsonModel fixes the shape every frontmatter value must already be in
        before it reaches the engine: mapping -> System.Collections.Specialized.OrderedDictionary
        (string keys), sequence -> object?[], scalar -> string/long/double/bool/null — nothing
        else (JsonModel.ToJsonNode throws NotSupportedException for anything outside that set).
        This is the write-cmdlet side of that contract: PowerShell integers/decimals become
        long/double, IDictionary/PSCustomObject become OrderedDictionary, and any other
        enumerable becomes object?[], all recursively.
    #>
    [CmdletBinding()]
    [OutputType([object])]
    param(
        [Parameter(ValueFromPipeline)]
        [AllowNull()]
        [object]$Value
    )

    process {
        $unwrapped = if ($Value -is [System.Management.Automation.PSObject]) { $Value.BaseObject } else { $Value }

        switch ($unwrapped) {
            { $null -eq $_ } { return $null }
            { $_ -is [bool] } { return [bool]$_ }
            { $_ -is [string] } { return [string]$_ }
            { $_ -is [System.Collections.IDictionary] } {
                $result = [System.Collections.Specialized.OrderedDictionary]::new()
                foreach ($key in $_.Keys) {
                    $result[[string]$key] = ConvertTo-MdbFrontmatterValue -Value $_[$key]
                }

                return $result
            }
            { $_ -is [sbyte] -or $_ -is [byte] -or $_ -is [int16] -or $_ -is [uint16] -or $_ -is [int32] -or $_ -is [uint32] -or $_ -is [int64] -or $_ -is [uint64] } {
                return [long]$_
            }
            { $_ -is [single] -or $_ -is [double] -or $_ -is [decimal] } {
                return [double]$_
            }
            { $_ -is [System.Management.Automation.PSCustomObject] } {
                $result = [System.Collections.Specialized.OrderedDictionary]::new()
                foreach ($property in $_.PSObject.Properties) {
                    $result[$property.Name] = ConvertTo-MdbFrontmatterValue -Value $property.Value
                }

                return $result
            }
            { $_ -is [System.Collections.IEnumerable] } {
                $items = [System.Collections.Generic.List[object]]::new()
                foreach ($item in $_) {
                    $items.Add((ConvertTo-MdbFrontmatterValue -Value $item))
                }

                return $items.ToArray()
            }
            default {
                throw [System.NotSupportedException]::new(
                    "Frontmatter value of type '$($unwrapped.GetType().FullName)' is not part of the mdbase JSON data model (supported: string, bool, integer, float, mapping, sequence, null).")
            }
        }
    }
}

function ConvertTo-MdbFrontmatter {
    <#
    .SYNOPSIS
        Converts a top-level frontmatter IDictionary into an OrderedDictionary Mdbase.Core accepts.
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Frontmatter
    )

    ConvertTo-MdbFrontmatterValue -Value $Frontmatter
}
