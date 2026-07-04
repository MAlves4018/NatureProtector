@{
    Severity = @('Error', 'Warning')
    IncludeDefaultRules = $true
    ExcludeRules = @(
        # Legacy scripts use approved verbs and local credential variable names
        # that require contextual review before enforcement.
        'PSAvoidUsingConvertToSecureStringWithPlainText',
        'PSUseShouldProcessForStateChangingFunctions'
    )
    Rules = @{
        PSAvoidUsingCmdletAliases = @{
            Whitelist = @('where', 'foreach')
        }
        PSUseConsistentIndentation = @{
            Enable = $true
            Kind = 'space'
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            IndentationSize = 4
        }
        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $false
            CheckPipe = $true
            CheckPipeForRedundantWhitespace = $false
            CheckSeparator = $true
            CheckParameter = $false
        }
    }
}
