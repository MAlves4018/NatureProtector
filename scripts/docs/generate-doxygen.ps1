param(
    [ValidateSet("fast", "medium", "heavy")]
    [string] $Profile = "fast",
    [switch] $Diagnostic,
    [switch] $KeepTempConfig,
    [switch] $NoDot,
    [switch] $SrcOnly,
    [switch] $DocsOnly,
    [string] $MarkdownSubset = "all"
)

Import-Module (Join-Path $PSScriptRoot '../common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"

function Convert-ToDoxygenPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    return (Get-NpAbsolutePath -Path $Path).Replace('\', '/')
}

function Clear-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
        return
    }

    Get-ChildItem -LiteralPath $Path -Force |
        Where-Object { $_.Name -ne ".gitkeep" } |
        Remove-Item -Recurse -Force
}

function Test-IsExcludedInputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string[]] $ExcludedPrefixes
    )

    $normalizedPath = (Get-NpAbsolutePath -Path $Path).Replace('\', '/')

    foreach ($prefix in $ExcludedPrefixes) {
        if ($normalizedPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return (
        $normalizedPath -match '/(bin|obj|TestResults|\.git|\.idea|\.nuget)/' -or
        $normalizedPath -match '/coveragereport_[^/]+/' -or
        $normalizedPath -match '/(html|xml|latex|rtf|man|docbook)/' -or
        $normalizedPath -match '/Migrations/.*\.Designer\.cs$' -or
        $normalizedPath -match '/Migrations/NatureProtectorControlDbContextModelSnapshot\.cs$' -or
        $normalizedPath -match '/Properties/AssemblyInfo\.cs$'
    )
}

function Get-DoxygenInputFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RootPath,
        [Parameter(Mandatory = $true)]
        [string[]] $Extensions,
        [Parameter(Mandatory = $true)]
        [string[]] $ExcludedPrefixes
    )

    $allowedExtensions = $Extensions | ForEach-Object { $_.ToLowerInvariant() }

    return Get-ChildItem -LiteralPath $RootPath -Recurse -File -Force |
        Where-Object {
            $_.Extension.ToLowerInvariant() -in $allowedExtensions -and
            -not (Test-IsExcludedInputPath -Path $_.FullName -ExcludedPrefixes $ExcludedPrefixes)
        } |
        Select-Object -ExpandProperty FullName |
        Sort-Object -Unique
}

function Convert-ToRepoRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $absolutePath = Get-NpAbsolutePath -Path $Path
    $absoluteRepoRoot = (Get-NpAbsolutePath -Path $RepoRoot).TrimEnd('\')

    if ($absolutePath.StartsWith($absoluteRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relativePath = $absolutePath.Substring($absoluteRepoRoot.Length).TrimStart('\', '/')
        return $relativePath.Replace('\', '/')
    }

    return $absolutePath.Replace('\', '/')
}

function Resolve-MarkdownSubset {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $MarkdownFiles,
        [Parameter(Mandatory = $true)]
        [string] $Subset,
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot
    )

    $sortedMarkdownFiles = @($MarkdownFiles | Sort-Object -Unique)
    $normalizedSubset = if ([string]::IsNullOrWhiteSpace($Subset)) { "all" } else { $Subset.Trim() }

    if (-not $sortedMarkdownFiles) {
        return [PSCustomObject]@{
            RequestedSubset = $normalizedSubset
            IncludedFiles = @()
            ExcludedFiles = @()
        }
    }

    $markdownByRelativePath = @{}
    $markdownByAbsolutePath = @{}

    foreach ($markdownFile in $sortedMarkdownFiles) {
        $relativePath = Convert-ToRepoRelativePath -Path $markdownFile -RepoRoot $RepoRoot
        $absolutePath = (Get-NpAbsolutePath -Path $markdownFile).Replace('\', '/')
        $markdownByRelativePath[$relativePath] = $markdownFile
        $markdownByAbsolutePath[$absolutePath] = $markdownFile
    }

    $includedFiles = switch -Regex ($normalizedSubset) {
        '^all$' {
            $sortedMarkdownFiles
            break
        }
        '^groupA$' {
            $groupSize = [int][Math]::Ceiling($sortedMarkdownFiles.Count / 2.0)
            @($sortedMarkdownFiles | Select-Object -First $groupSize)
            break
        }
        '^groupB$' {
            $groupSize = [int][Math]::Ceiling($sortedMarkdownFiles.Count / 2.0)
            @($sortedMarkdownFiles | Select-Object -Skip $groupSize)
            break
        }
        '^single:(.+)$' {
            $requestedPath = $Matches[1].Trim()

            if ([string]::IsNullOrWhiteSpace($requestedPath)) {
                throw "MarkdownSubset 'single:' requires a markdown path."
            }

            $normalizedRequestedPath = $requestedPath.Replace('\', '/').Trim()
            $candidateAbsolutePath = (Get-NpAbsolutePath -Path (Join-Path $RepoRoot $requestedPath)).Replace('\', '/')

            if ($markdownByRelativePath.ContainsKey($normalizedRequestedPath)) {
                @($markdownByRelativePath[$normalizedRequestedPath])
            }
            elseif ($markdownByAbsolutePath.ContainsKey($normalizedRequestedPath)) {
                @($markdownByAbsolutePath[$normalizedRequestedPath])
            }
            elseif ($markdownByAbsolutePath.ContainsKey($candidateAbsolutePath)) {
                @($markdownByAbsolutePath[$candidateAbsolutePath])
            }
            else {
                throw "MarkdownSubset '$normalizedSubset' did not match any markdown input. Expected one of: $($markdownByRelativePath.Keys -join ', ')"
            }

            break
        }
        '^list:(.+)$' {
            $requestedPaths = @(
                $Matches[1].Split(',', [System.StringSplitOptions]::RemoveEmptyEntries) |
                    ForEach-Object { $_.Trim() } |
                    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )

            if (-not $requestedPaths) {
                throw "MarkdownSubset 'list:' requires at least one markdown path."
            }

            $resolvedSelection = New-Object System.Collections.Generic.List[string]

            foreach ($requestedPath in $requestedPaths) {
                $normalizedRequestedPath = $requestedPath.Replace('\', '/').Trim()
                $candidateAbsolutePath = (Get-NpAbsolutePath -Path (Join-Path $RepoRoot $requestedPath)).Replace('\', '/')

                if ($markdownByRelativePath.ContainsKey($normalizedRequestedPath)) {
                    $resolvedSelection.Add($markdownByRelativePath[$normalizedRequestedPath])
                }
                elseif ($markdownByAbsolutePath.ContainsKey($normalizedRequestedPath)) {
                    $resolvedSelection.Add($markdownByAbsolutePath[$normalizedRequestedPath])
                }
                elseif ($markdownByAbsolutePath.ContainsKey($candidateAbsolutePath)) {
                    $resolvedSelection.Add($markdownByAbsolutePath[$candidateAbsolutePath])
                }
                else {
                    throw "MarkdownSubset '$normalizedSubset' contains an unknown markdown path: $requestedPath"
                }
            }

            @($resolvedSelection | Sort-Object -Unique)
            break
        }
        default {
            throw "Unsupported MarkdownSubset '$normalizedSubset'. Supported values: all, groupA, groupB, single:<path>, list:<comma-separated-paths>"
        }
    }

    $includedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($includedFile in $includedFiles) {
        [void]$includedSet.Add($includedFile)
    }

    $excludedFiles = @(
        $sortedMarkdownFiles |
            Where-Object { -not $includedSet.Contains($_) }
    )

    return [PSCustomObject]@{
        RequestedSubset = $normalizedSubset
        IncludedFiles = @($includedFiles | Sort-Object -Unique)
        ExcludedFiles = $excludedFiles
    }
}

function New-InputRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Label,
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [bool] $ExpectDirectory,
        [string[]] $Extensions = @()
    )

    return [PSCustomObject]@{
        Label = $Label
        Path = $Path
        ExpectDirectory = $ExpectDirectory
        Extensions = $Extensions
    }
}

function Get-ProfileInputRoots {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SelectedProfile
    )

    $fastRoots = @(
        (New-InputRoot -Label "src" -Path (Join-Path $repoRoot "src") -ExpectDirectory $true -Extensions @(".cs")),
        (New-InputRoot -Label "README.md" -Path (Join-Path $repoRoot "README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "tests/README.md" -Path (Join-Path $repoRoot "tests\README.md") -ExpectDirectory $false)
    )

    $mediumRoots = @(
        (New-InputRoot -Label "src" -Path (Join-Path $repoRoot "src") -ExpectDirectory $true -Extensions @(".cs")),
        (New-InputRoot -Label "README.md" -Path (Join-Path $repoRoot "README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "tests/README.md" -Path (Join-Path $repoRoot "tests\README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "docs/doxygen/pages" -Path (Join-Path $docsRoot "doxygen\pages") -ExpectDirectory $true -Extensions @(".md"))
    )

    $heavyRoots = @(
        (New-InputRoot -Label "src" -Path (Join-Path $repoRoot "src") -ExpectDirectory $true -Extensions @(".cs")),
        (New-InputRoot -Label "README.md" -Path (Join-Path $repoRoot "README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "tests/README.md" -Path (Join-Path $repoRoot "tests\README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "docs/doxygen/pages" -Path (Join-Path $docsRoot "doxygen\pages") -ExpectDirectory $true -Extensions @(".md")),
        (New-InputRoot -Label "docs/architecture" -Path (Join-Path $docsRoot "architecture") -ExpectDirectory $true -Extensions @(".md")),
        (New-InputRoot -Label "data/README.md" -Path (Join-Path $repoRoot "data\README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "scripts/data/README.md" -Path (Join-Path $repoRoot "scripts\data\README.md") -ExpectDirectory $false),
        (New-InputRoot -Label "infra/README.md" -Path (Join-Path $repoRoot "infra\README.md") -ExpectDirectory $false)
    )

    switch ($SelectedProfile) {
        "fast" { return $fastRoots }
        "medium" { return $mediumRoots }
        "heavy" { return $heavyRoots }
        default { throw "Unsupported profile: $SelectedProfile" }
    }
}

function Get-ProfileOverrides {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SelectedProfile,
        [Parameter(Mandatory = $true)]
        [bool] $DisableDot
    )

    $settings = [ordered]@{}

    switch ($SelectedProfile) {
        "fast" {
            $settings["HAVE_DOT"] = "NO"
            $settings["CALL_GRAPH"] = "NO"
            $settings["CALLER_GRAPH"] = "NO"
            $settings["DIRECTORY_GRAPH"] = "NO"
            $settings["CLASS_GRAPH"] = "NO"
            $settings["COLLABORATION_GRAPH"] = "NO"
            $settings["SEARCHENGINE"] = "NO"
            $settings["SOURCE_BROWSER"] = "NO"
            $settings["REFERENCED_BY_RELATION"] = "NO"
            $settings["REFERENCES_RELATION"] = "NO"
            $settings["VERBATIM_HEADERS"] = "NO"
            $settings["GRAPHICAL_HIERARCHY"] = "NO"
            $settings["GROUP_GRAPHS"] = "NO"
        }
        "medium" {
            $settings["HAVE_DOT"] = "YES"
            $settings["CALL_GRAPH"] = "NO"
            $settings["CALLER_GRAPH"] = "NO"
            $settings["DIRECTORY_GRAPH"] = "NO"
            $settings["CLASS_GRAPH"] = "YES"
            $settings["COLLABORATION_GRAPH"] = "YES"
            $settings["SEARCHENGINE"] = "YES"
            $settings["SOURCE_BROWSER"] = "YES"
            $settings["REFERENCED_BY_RELATION"] = "YES"
            $settings["REFERENCES_RELATION"] = "YES"
            $settings["VERBATIM_HEADERS"] = "YES"
            $settings["GRAPHICAL_HIERARCHY"] = "YES"
            $settings["GROUP_GRAPHS"] = "YES"
        }
        "heavy" {
            $settings["HAVE_DOT"] = "YES"
            $settings["CALL_GRAPH"] = "YES"
            $settings["CALLER_GRAPH"] = "YES"
            $settings["DIRECTORY_GRAPH"] = "YES"
            $settings["CLASS_GRAPH"] = "YES"
            $settings["COLLABORATION_GRAPH"] = "YES"
            $settings["SEARCHENGINE"] = "YES"
            $settings["SOURCE_BROWSER"] = "YES"
            $settings["REFERENCED_BY_RELATION"] = "YES"
            $settings["REFERENCES_RELATION"] = "YES"
            $settings["VERBATIM_HEADERS"] = "YES"
            $settings["GRAPHICAL_HIERARCHY"] = "YES"
            $settings["GROUP_GRAPHS"] = "YES"
        }
    }

    if ($DisableDot) {
        $settings["HAVE_DOT"] = "NO"
        $settings["CALL_GRAPH"] = "NO"
        $settings["CALLER_GRAPH"] = "NO"
        $settings["DIRECTORY_GRAPH"] = "NO"
        $settings["CLASS_GRAPH"] = "NO"
        $settings["COLLABORATION_GRAPH"] = "NO"
        $settings["GRAPHICAL_HIERARCHY"] = "NO"
        $settings["GROUP_GRAPHS"] = "NO"
    }

    return $settings
}

if ($SrcOnly -and $DocsOnly) {
    throw "The switches -SrcOnly and -DocsOnly are mutually exclusive."
}

$overallStart = Get-Date
$invocationWorkingDirectory = (Get-Location).Path
$repoRoot = Get-NpAbsolutePath -Path (Join-Path $PSScriptRoot "..\..")
$docsRoot = Join-Path $repoRoot "docs"
$doxygenRoot = Join-Path $docsRoot "doxygen"
$doxyConfig = Join-Path $doxygenRoot "config\Doxyfile"
$doxyOutput = Join-Path $doxygenRoot "output"
$effectiveConfigPath = Join-Path $doxyOutput "effective.Doxyfile"
$summaryPath = Join-Path $doxyOutput "last-run-summary.txt"
$mainPage = Join-Path $doxygenRoot "pages\mainpage.md"
$docfxRoot = Join-Path $docsRoot "docfx"
$structurizrOutputRoot = Join-Path $docsRoot "structurizr\output"

Assert-NpPathExists -Path $repoRoot -Description "Repository root" -ExpectDirectory $true
Assert-NpPathExists -Path $doxyConfig -Description "Base Doxygen configuration" -ExpectDirectory $false

foreach ($directory in @($doxygenRoot, $doxyOutput)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$doxygenCommand = Get-Command doxygen -ErrorAction SilentlyContinue
if (-not $doxygenCommand) {
    throw "The 'doxygen' executable is not available on PATH."
}

$baseInputRoots = Get-ProfileInputRoots -SelectedProfile $Profile

if ($SrcOnly) {
    $selectedInputRoots = $baseInputRoots | Where-Object { $_.Label -eq "src" }
}
elseif ($DocsOnly) {
    $selectedInputRoots = $baseInputRoots | Where-Object { $_.Label -ne "src" }
}
else {
    $selectedInputRoots = $baseInputRoots
}

foreach ($inputRoot in $selectedInputRoots) {
    Assert-NpPathExists -Path $inputRoot.Path -Description "INPUT $($inputRoot.Label)" -ExpectDirectory $inputRoot.ExpectDirectory
}

$pagesIncluded = @($selectedInputRoots | Where-Object { $_.Label -eq "docs/doxygen/pages" }).Count -gt 0
if ($pagesIncluded) {
    Assert-NpPathExists -Path $mainPage -Description "Doxygen main page" -ExpectDirectory $false
}

$excludedPrefixes = @(
    ((Convert-ToDoxygenPath -Path $doxyOutput).TrimEnd('/') + '/'),
    ((Convert-ToDoxygenPath -Path $docfxRoot).TrimEnd('/') + '/'),
    ((Convert-ToDoxygenPath -Path $structurizrOutputRoot).TrimEnd('/') + '/')
)

$cleanupStart = Get-Date
Clear-DirectoryContents -Path $doxyOutput
$cleanupEnd = Get-Date

$prepStart = Get-Date
$resolvedInputFiles = New-Object System.Collections.Generic.List[string]
$includedRootLabels = New-Object System.Collections.Generic.List[string]

foreach ($inputRoot in $selectedInputRoots) {
    $includedRootLabels.Add($inputRoot.Label)

    if ($inputRoot.ExpectDirectory) {
        $files = Get-DoxygenInputFiles -RootPath $inputRoot.Path -Extensions $inputRoot.Extensions -ExcludedPrefixes $excludedPrefixes

        if (-not $files) {
            throw "INPUT $($inputRoot.Label) resolved to zero files: $($inputRoot.Path)"
        }

        foreach ($file in $files) {
            $resolvedInputFiles.Add($file)
        }
    }
    else {
        if (Test-IsExcludedInputPath -Path $inputRoot.Path -ExcludedPrefixes $excludedPrefixes) {
            throw "INPUT $($inputRoot.Label) resolved to an excluded file: $($inputRoot.Path)"
        }

        $resolvedInputFiles.Add((Get-NpAbsolutePath -Path $inputRoot.Path))
    }
}

$resolvedInputFiles = $resolvedInputFiles | Sort-Object -Unique

if (-not $resolvedInputFiles) {
    throw "No Doxygen inputs were resolved."
}

$resolvedCsInputFiles = @(
    $resolvedInputFiles |
        Where-Object { $_.EndsWith(".cs", [System.StringComparison]::OrdinalIgnoreCase) }
)
$resolvedMarkdownCandidates = @(
    $resolvedInputFiles |
        Where-Object { $_.EndsWith(".md", [System.StringComparison]::OrdinalIgnoreCase) }
)

if ($resolvedMarkdownCandidates.Count -eq 0) {
    $markdownSubsetResult = [PSCustomObject]@{
        RequestedSubset = $MarkdownSubset
        IncludedFiles = @()
        ExcludedFiles = @()
    }
}
else {
    $markdownSubsetResult = Resolve-MarkdownSubset -MarkdownFiles $resolvedMarkdownCandidates -Subset $MarkdownSubset -RepoRoot $repoRoot
}

$resolvedMarkdownInputFiles = @($markdownSubsetResult.IncludedFiles)
$excludedMarkdownInputFiles = @($markdownSubsetResult.ExcludedFiles)

$resolvedInputFiles = @($resolvedCsInputFiles + $resolvedMarkdownInputFiles) | Sort-Object -Unique

if (-not $resolvedInputFiles) {
    throw "No Doxygen inputs remained after applying MarkdownSubset '$($markdownSubsetResult.RequestedSubset)'."
}

if ($DocsOnly -and -not $resolvedMarkdownInputFiles) {
    throw "MarkdownSubset '$($markdownSubsetResult.RequestedSubset)' resolved to zero markdown files for a DocsOnly run."
}

$resolvedCsFiles = $resolvedCsInputFiles.Count
$resolvedMarkdownFiles = $resolvedMarkdownInputFiles.Count
$excludedMarkdownFiles = $excludedMarkdownInputFiles.Count

$profileOverrides = Get-ProfileOverrides -SelectedProfile $Profile -DisableDot $NoDot.IsPresent
$dotEnabled = $profileOverrides["HAVE_DOT"] -eq "YES"
$overrideLines = $profileOverrides.GetEnumerator() | ForEach-Object {
    "{0,-22} = {1}" -f $_.Key, $_.Value
}

$templateContent = Get-Content -LiteralPath $doxyConfig -Raw
$absoluteInputPaths = $resolvedInputFiles | ForEach-Object { '"{0}"' -f (Convert-ToDoxygenPath -Path $_) }
$inputBlock = ($absoluteInputPaths -join " `\`r`n                         ")
$outputPathForDoxygen = Convert-ToDoxygenPath -Path $doxyOutput
$profileBlock = $overrideLines -join "`r`n"
$mainPageLine = if ($pagesIncluded) {
    'USE_MDFILE_AS_MAINPAGE = "{0}"' -f (Convert-ToDoxygenPath -Path $mainPage)
}
else {
    "USE_MDFILE_AS_MAINPAGE ="
}

$tempConfig = Join-Path ([System.IO.Path]::GetTempPath()) ("NatureProtector.Doxyfile.{0}.tmp" -f [System.Guid]::NewGuid().ToString("N"))
$tempContent = @"
$templateContent

OUTPUT_DIRECTORY       = $outputPathForDoxygen
INPUT                  = $inputBlock
$mainPageLine
STRIP_FROM_PATH        = "$($repoRoot.Replace('\', '/'))"
$profileBlock
"@

Set-Content -LiteralPath $tempConfig -Value $tempContent -Encoding ASCII
Set-Content -LiteralPath $effectiveConfigPath -Value $tempContent -Encoding ASCII
$prepEnd = Get-Date

Write-Host "Generating Doxygen documentation from $repoRoot"
Write-Host "Doxygen profile: $Profile"
Write-Host "Markdown subset: $($markdownSubsetResult.RequestedSubset)"
Write-Host ("Diagnostic mode: {0}" -f ($(if ($Diagnostic) { "on" } else { "off" })))
Write-Host ("Dot enabled: {0}" -f ($(if ($dotEnabled) { "yes" } else { "no" })))
Write-Host "Resolved C# files: $resolvedCsFiles"
Write-Host "Resolved Markdown files: $resolvedMarkdownFiles"
Write-Host "Excluded Markdown files: $excludedMarkdownFiles"
Write-Host "Resolved input entries: $($resolvedInputFiles.Count)"
Write-Host "Effective config written to $effectiveConfigPath"

if ($Diagnostic) {
    Write-Host "Markdown files included:"
    foreach ($input in $resolvedMarkdownInputFiles) {
        Write-Host "  $input"
    }

    Write-Host "Markdown files excluded:"
    foreach ($input in $excludedMarkdownInputFiles) {
        Write-Host "  $input"
    }

    Write-Host "First resolved inputs (max 30):"
    foreach ($input in ($resolvedInputFiles | Select-Object -First 30)) {
        Write-Host "  $input"
    }
}

$execStart = Get-Date
$exitCode = -1
$pushedLocation = $false

try {
    Push-Location $repoRoot
    $pushedLocation = $true
    & $doxygenCommand.Source $tempConfig
    $exitCode = if ($LASTEXITCODE -is [int]) { $LASTEXITCODE } else { 0 }
}
finally {
    $execEnd = Get-Date

    if ($pushedLocation) {
        Pop-Location
    }

    if (-not $KeepTempConfig -and (Test-Path -LiteralPath $tempConfig)) {
        Remove-Item -LiteralPath $tempConfig -Force
    }
}

$overallEnd = Get-Date
$cleanupDuration = $cleanupEnd - $cleanupStart
$prepDuration = $prepEnd - $prepStart
$execDuration = $execEnd - $execStart
$overallDuration = $overallEnd - $overallStart

$summaryLines = @(
    "timestamp=$($overallEnd.ToString("o"))",
    "profile=$Profile",
    "markdown_subset=$($markdownSubsetResult.RequestedSubset)",
    "diagnostic=$([bool]$Diagnostic)",
    "keep_temp_config=$([bool]$KeepTempConfig)",
    "no_dot=$([bool]$NoDot)",
    "src_only=$([bool]$SrcOnly)",
    "docs_only=$([bool]$DocsOnly)",
    "current_working_directory=$invocationWorkingDirectory",
    "repo_root=$repoRoot",
    "output_dir=$doxyOutput",
    "effective_config=$effectiveConfigPath",
    "temp_config=$tempConfig",
    "dot_enabled=$dotEnabled",
    "input_count=$($resolvedInputFiles.Count)",
    "input_cs_count=$resolvedCsFiles",
    "input_md_count=$resolvedMarkdownFiles",
    "input_md_excluded_count=$excludedMarkdownFiles",
    "included_roots=$($includedRootLabels -join ', ')",
    "markdown_included=$($(($resolvedMarkdownInputFiles | ForEach-Object { Convert-ToRepoRelativePath -Path $_ -RepoRoot $repoRoot }) -join ', '))",
    "markdown_excluded=$($(($excludedMarkdownInputFiles | ForEach-Object { Convert-ToRepoRelativePath -Path $_ -RepoRoot $repoRoot }) -join ', '))",
    "flags=$($overrideLines -join ' | ')",
    "cleanup_duration=$cleanupDuration",
    "preparation_duration=$prepDuration",
    "execution_duration=$execDuration",
    "total_duration=$overallDuration",
    "exit_code=$exitCode"
)

Set-Content -LiteralPath $summaryPath -Value $summaryLines -Encoding UTF8

Write-Host "Summary written to $summaryPath"
Write-Host "Execution duration: $execDuration"
Write-Host "Total duration: $overallDuration"
Write-Host "Doxygen exit code: $exitCode"

if ($KeepTempConfig) {
    Write-Host "Temporary config kept at $tempConfig"
}

if ($exitCode -ne 0) {
    throw "Doxygen exited with code $exitCode"
}

Write-Host "Documentation generated at $($doxyOutput)\html\index.html"
