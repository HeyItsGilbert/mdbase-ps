function ConvertTo-MdbErrorRecord {
    <#
    .SYNOPSIS
        Translates an Mdbase.Core exception into a terminating-error-ready ErrorRecord.
    .DESCRIPTION
        The one shared translation point every write/query cmdlet funnels its own catch block
        through (#41/#42 point 24): the resulting ErrorRecord's Exception message is the
        diagnostic's Message, and its TargetObject carries the full Mdbase.Core.MdbDiagnostic
        (synthesized for the two query exceptions, which carry no diagnostic of their own) so
        `$Error[0].TargetObject` stays programmatically inspectable — matching how Mdbase.Core
        itself never throws a bare string exception.
    .PARAMETER Exception
        The caught exception. Recognizes MdbWriteException, MdbInvalidQueryException,
        MdbQueryContextNotFoundException, and MdbCollectionNotFoundException specially; any other
        exception is wrapped in a synthesized generic diagnostic.
    .PARAMETER ErrorId
        Overrides the ErrorRecord's FullyQualifiedErrorId; defaults to the diagnostic's Code.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.ErrorRecord])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [System.Exception]$Exception,

        [string]$ErrorId
    )

    process {
        # A generic (untyped) catch block sees the .NET method-invocation wrapper PowerShell
        # puts around a failing dynamic method call, not the exception Mdbase.Core actually
        # threw — typed `catch [Mdbase.Core.MdbWriteException]` clauses unwrap this
        # automatically when matching, but Find-MdbRecord's multi-exception-type catch cannot,
        # so unwrap explicitly here before classifying.
        $resolvedException = $Exception
        while ($resolvedException -is [System.Management.Automation.MethodInvocationException] -and $null -ne $resolvedException.InnerException) {
            $resolvedException = $resolvedException.InnerException
        }

        $diagnostic = switch ($resolvedException) {
            { $_ -is [Mdbase.Core.MdbWriteException] } {
                $_.Diagnostic
                break
            }
            { $_ -is [Mdbase.Core.Query.MdbInvalidQueryException] } {
                [Mdbase.Core.MdbDiagnostic]@{
                    Severity = [Mdbase.Core.MdbSeverity]::Error
                    Code     = 'invalid_query'
                    Message  = $_.Message
                }
                break
            }
            { $_ -is [Mdbase.Core.Query.MdbQueryContextNotFoundException] } {
                [Mdbase.Core.MdbDiagnostic]@{
                    Severity = [Mdbase.Core.MdbSeverity]::Error
                    Code     = 'context_not_found'
                    Message  = $_.Message
                }
                break
            }
            { $_ -is [Mdbase.Core.MdbCollectionNotFoundException] } {
                [Mdbase.Core.MdbDiagnostic]@{
                    Severity = [Mdbase.Core.MdbSeverity]::Error
                    Code     = 'collection_not_found'
                    Message  = $_.Message
                    Path     = $_.Path
                }
                break
            }
            default {
                [Mdbase.Core.MdbDiagnostic]@{
                    Severity = [Mdbase.Core.MdbSeverity]::Error
                    Code     = 'error'
                    Message  = $_.Message
                }
            }
        }

        $resolvedErrorId = if ($ErrorId) { $ErrorId } else { $diagnostic.Code }
        $errorRecord = [System.Management.Automation.ErrorRecord]::new(
            [System.Exception]::new($diagnostic.Message, $Exception),
            $resolvedErrorId,
            [System.Management.Automation.ErrorCategory]::InvalidOperation,
            $diagnostic
        )
        $errorRecord.ErrorDetails = [System.Management.Automation.ErrorDetails]::new($diagnostic.Message)
        $errorRecord
    }
}
