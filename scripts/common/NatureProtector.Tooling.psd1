@{
    RootModule = 'NatureProtector.Tooling.psm1'
    ModuleVersion = '1.0.0'
    GUID = '03bce8da-b13f-44f1-8f19-fd9c1f667ca3'
    Author = 'NatureProtector'
    CompanyName = 'NatureProtector'
    Copyright = '(c) NatureProtector'
    Description = 'Shared, side-effect-free PowerShell primitives for repository automation.'
    PowerShellVersion = '7.0'
    FunctionsToExport = @(
        'Find-NpRepositoryRoot',
        'Read-NpDotEnv',
        'Get-NpConfigValue',
        'Get-NpRelativePath',
        'Get-NpPathUnderRoot',
        'Invoke-NpExternalCommand',
        'Test-NpTcpEndpoint',
        'Resolve-NpValidationPython',
        'Write-NpJsonFile',
        'Get-NpAbsolutePath',
        'Assert-NpPathExists',
        'Get-NpFreeTcpPort',
        'Get-NpCommandLineVersion',
        'Get-NpPercentileNearestRank'
    )
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('NatureProtector', 'Tooling', 'Automation')
        }
    }
}
