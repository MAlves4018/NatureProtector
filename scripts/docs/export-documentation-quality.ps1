<#
.SYNOPSIS
Exports a documentation quality inventory for Markdown files.

.DESCRIPTION
This script scans current documentation, test documentation and project
README files for encoding artefacts and claims that require manual review.
It does not execute Git commands and does not mutate source documentation.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputRoot,
    [switch]$FailOnMojibake,
    [switch]$FailOnCanonicalMojibake,
    [switch]$FailOnCanonicalDefects,
    [string[]]$CorrectedFile = @()
)

$ErrorActionPreference = "Stop"

$normalizedCorrectedFiles = @(
    foreach ($entry in $CorrectedFile) {
        if ([string]::IsNullOrWhiteSpace($entry)) {
            continue
        }

        $entry -split "," | ForEach-Object {
            $trimmed = $_.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $trimmed
            }
        }
    }
)

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputRoot = Join-Path $RepositoryRoot "artifacts\validation\documentation-quality\$timestamp"
}

$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$resolvedOutputRoot = $OutputRoot
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$scanRoots = @(
    "README.md",
    "docs",
    "tests",
    "src"
)

$excludedPathFragments = @(
    "\bin\",
    "\obj\",
    "\TestResults\",
    "\artifacts\",
    "\node_modules\",
    "\coverage\",
    "\playwright-report\"
)

$mojibakeLeadingA = [regex]::Escape([string][char]0x00c3)
$mojibakeLeadingE = [regex]::Escape([string][char]0x00e2)
$mojibakeEuro = [regex]::Escape([string][char]0x20ac)
$mojibakeDagger = [regex]::Escape([string][char]0x2020)
$replacementCharacter = [regex]::Escape([string][char]0xfffd)

$mojibakePatterns = @(
    @{ Name = "utf8_as_cp1252"; Pattern = "$mojibakeLeadingA|$mojibakeLeadingE($mojibakeEuro|$mojibakeDagger)" },
    @{ Name = "replacement_character"; Pattern = $replacementCharacter }
)

$claimPatterns = @(
    @{ Name = "production_readiness"; Pattern = "production readiness|prod readiness" },
    @{ Name = "stress_or_load_test"; Pattern = "stress test|load test|stress testing|load testing" },
    @{ Name = "fullstack_e2e"; Pattern = "FullStackE2E|full-stack e2e|end-to-end" },
    @{ Name = "clean_install_scope"; Pattern = "functional clean|clean install" },
    @{ Name = "observability_delivery"; Pattern = "delivery proof|delivery validated|collector OTLP|live collector" },
    @{ Name = "performance_scope"; Pattern = "system performance|performance sist|microbenchmark|BenchmarkDotNet" },
    @{ Name = "block_closure"; Pattern = "Bloco C|Bloco F|Bloco G|fechado|closed|PROVED|proved" }
)

function Get-DocumentScope {
    param([string]$RelativePath)

    $path = $RelativePath.ToLowerInvariant()

    if ($path.StartsWith("docs\evidence\")) {
        return "EvidenceHistorical"
    }

    if ($path.StartsWith("docs\planning\")) {
        return "HistoricalPlanning"
    }

    if ($path.StartsWith("docs\doxygen\") -or $path.StartsWith("docs\docfx\")) {
        return "GeneratedReference"
    }

    if (
        $path -eq "readme.md" -or
        $path.StartsWith("docs\setup\") -or
        $path.StartsWith("docs\implementation\") -or
        $path.StartsWith("docs\contracts\") -or
        $path -eq "docs\runtime-developer-control.md" -or
        $path -eq "docs\natureprotector-v1-overview.md" -or
        $path -eq "tests\readme.md"
    ) {
        return "CanonicalOperational"
    }

    if ($path.StartsWith("src\") -and $path.EndsWith("\readme.md")) {
        return "RuntimeReadme"
    }

    return "SecondaryDocumentation"
}

function Test-CanonicalScope {
    param([string]$DocumentScope)

    return $DocumentScope -eq "CanonicalOperational" -or $DocumentScope -eq "RuntimeReadme"
}

function Test-NegatedClaimLine {
    param([string]$Line)

    $normalized = $Line.Normalize([System.Text.NormalizationForm]::FormD)
    $builder = [System.Text.StringBuilder]::new()
    foreach ($character in $normalized.ToCharArray()) {
        if ([System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($character) -ne [System.Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($character)
        }
    }

    $lower = $builder.ToString().ToLowerInvariant()
    return (
        $lower -match "\bnot\b" -or
        $lower -match "does not" -or
        $lower -match "do not" -or
        $lower -match "\bno\b" -or
        $lower -match "\bsem\b" -or
        $lower -match "\bnao\b" -or
        $lower -match "nao deve" -or
        $lower -match "nao substitui" -or
        $lower -match "nao e "
    )
}

function Get-FindingClassification {
    param(
        [string]$Category,
        [string]$Rule,
        [string]$DocumentScope,
        [string]$Line
    )

    if ($DocumentScope -eq "EvidenceHistorical") {
        return "EVIDENCE_HISTORICA"
    }

    if ($DocumentScope -eq "HistoricalPlanning") {
        return "DOCUMENTO_HISTORICO"
    }

    if ($Category -eq "Encoding") {
        return "DEFEITO_REAL"
    }

    if ($Line -match '`(FullStackE2E|PROVED|BenchmarkDotNet|LOCAL_CAPACITY_BASELINE_REPRODUCIBLE|BLOCKED_AFTER_REMEDIATION_ATTEMPT|IMPLEMENTED_NOT_PROVED_REMOTELY|PROVED_LOCALLY_WITH_LIMITATIONS)`') {
        return "IDENTIFICADOR_ESTAVEL"
    }

    if ($Rule -eq "fullstack_e2e" -and $Line -match '`[^`]*end-to-end[^`]*`') {
        return "IDENTIFICADOR_ESTAVEL"
    }

    if (Test-NegatedClaimLine -Line $Line) {
        return "FALSO_POSITIVO"
    }

    if ($Rule -eq "block_closure" -and $Line -match "canal|conex") {
        return "TERMO_TECNICO_LEGITIMO"
    }

    if ($Rule -eq "clean_install_scope" -and $Line -match "test-clean-install|checksum|required paths|checksums\.sha256") {
        return "TERMO_TECNICO_LEGITIMO"
    }

    if ($Line -match "BenchmarkDotNet|microbenchmark|microbenchmarks|OpenTelemetry|OTLP|FullStackE2E|Playwright|GitHub Actions") {
        return "TERMO_TECNICO_LEGITIMO"
    }

    if (Test-CanonicalScope -DocumentScope $DocumentScope) {
        return "DEFEITO_REAL"
    }

    return "TERMO_TECNICO_LEGITIMO"
}

function Add-Finding {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$Severity,
        [string]$Category,
        [string]$Rule,
        [string]$File,
        [int]$Line,
        [string]$Text,
        [string]$DocumentScope
    )

    $classification = Get-FindingClassification -Category $Category -Rule $Rule -DocumentScope $DocumentScope -Line $Text

    $Findings.Add([pscustomobject]@{
        Severity = $Severity
        Category = $Category
        Rule = $Rule
        Classification = $classification
        DocumentScope = $DocumentScope
        IsCanonical = (Test-CanonicalScope -DocumentScope $DocumentScope)
        File = $File
        Line = $Line
        Text = $Text.Trim()
    }) | Out-Null
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseUri = [Uri]((Resolve-Path -LiteralPath $BasePath).Path.TrimEnd("\") + "\")
    $targetUri = [Uri](Resolve-Path -LiteralPath $TargetPath).Path
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace("/", "\")
}

function Test-ExcludedPath {
    param([string]$Path)

    $normalized = $Path.Replace("/", "\")
    foreach ($fragment in $excludedPathFragments) {
        if ($normalized.Contains($fragment)) {
            return $true
        }
    }

    return $false
}

$markdownFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($root in $scanRoots) {
    $path = Join-Path $resolvedRepositoryRoot $root
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $item = Get-Item -LiteralPath $path
    if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Filter "*.md" |
            Where-Object { -not (Test-ExcludedPath $_.FullName) } |
            ForEach-Object { $markdownFiles.Add($_) | Out-Null }
    }
    elseif ($item.Extension -ieq ".md") {
        $markdownFiles.Add($item) | Out-Null
    }
}

$findings = [System.Collections.Generic.List[object]]::new()

foreach ($file in ($markdownFiles | Sort-Object FullName -Unique)) {
    $relativePath = Get-RelativePath -BasePath $resolvedRepositoryRoot -TargetPath $file.FullName
    $documentScope = Get-DocumentScope -RelativePath $relativePath

    $strictUtf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true
    try {
        $text = [System.IO.File]::ReadAllText($file.FullName, $strictUtf8)
    }
    catch [System.Text.DecoderFallbackException] {
        $fallbackText = [System.IO.File]::ReadAllText($file.FullName)
        Add-Finding -Findings $findings `
            -Severity "High" `
            -Category "Encoding" `
            -Rule "invalid_utf8" `
            -File $relativePath `
            -Line 1 `
            -Text "File is not valid UTF-8 and was read with fallback encoding for inventory." `
            -DocumentScope $documentScope
        $text = $fallbackText
    }

    $lines = $text -split "\r?\n"

    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        $lineNumber = $index + 1

        foreach ($entry in $mojibakePatterns) {
            if ($line -cmatch $entry.Pattern) {
                Add-Finding -Findings $findings `
                    -Severity "High" `
                    -Category "Encoding" `
                    -Rule $entry.Name `
                    -File $relativePath `
                    -Line $lineNumber `
                    -Text $line `
                    -DocumentScope $documentScope
            }
        }

        foreach ($entry in $claimPatterns) {
            if ($line -match $entry.Pattern) {
                Add-Finding -Findings $findings `
                    -Severity "Review" `
                    -Category "ClaimScope" `
                    -Rule $entry.Name `
                    -File $relativePath `
                    -Line $lineNumber `
                    -Text $line `
                    -DocumentScope $documentScope
            }
        }
    }
}

$canonicalFindings = $findings | Where-Object { $_.IsCanonical -eq $true }
$canonicalEncodingFindings = $canonicalFindings | Where-Object { $_.Category -eq "Encoding" }
$canonicalDefectFindings = $canonicalFindings | Where-Object { $_.Classification -eq "DEFEITO_REAL" }
$classificationSummary = $findings |
    Group-Object Classification |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            classification = $_.Name
            count = $_.Count
        }
    }

$scopeSummary = $findings |
    Group-Object DocumentScope |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            documentScope = $_.Name
            count = $_.Count
        }
    }

$summary = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o")
    repositoryRoot = $resolvedRepositoryRoot
    scannedMarkdownFiles = ($markdownFiles | Sort-Object FullName -Unique).Count
    totalFindings = $findings.Count
    encodingFindings = ($findings | Where-Object { $_.Category -eq "Encoding" }).Count
    reviewFindings = ($findings | Where-Object { $_.Category -eq "ClaimScope" }).Count
    canonicalFindings = $canonicalFindings.Count
    canonicalEncodingFindings = $canonicalEncodingFindings.Count
    canonicalDefectFindings = $canonicalDefectFindings.Count
    classificationSummary = $classificationSummary
    scopeSummary = $scopeSummary
    outputRoot = $resolvedOutputRoot
    notes = @(
        "Review findings are inventory signals, not automatic defects.",
        "Historical evidence and planning files may intentionally contain older classifications.",
        "Encoding findings should be corrected in live documentation before final report material is produced.",
        "Canonical failures are opt-in via -FailOnCanonicalMojibake or -FailOnCanonicalDefects."
    )
}

$findingsJson = Join-Path $resolvedOutputRoot "findings.json"
$findingsCsv = Join-Path $resolvedOutputRoot "findings.csv"
$summaryJson = Join-Path $resolvedOutputRoot "summary.json"
$summaryMd = Join-Path $resolvedOutputRoot "summary.md"
$correctedFilesMd = Join-Path $resolvedOutputRoot "corrected-files.md"
$remainingReviewMd = Join-Path $resolvedOutputRoot "remaining-review.md"

ConvertTo-Json -InputObject $findings -Depth 5 | Set-Content -LiteralPath $findingsJson -Encoding UTF8
$findings | Export-Csv -LiteralPath $findingsCsv -NoTypeInformation -Encoding UTF8
ConvertTo-Json -InputObject $summary -Depth 5 | Set-Content -LiteralPath $summaryJson -Encoding UTF8

$topFiles = $findings |
    Group-Object File |
    Sort-Object Count -Descending |
    Select-Object -First 20

$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.Add("# Documentation quality inventory") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("Generated at: $($summary.generatedAt)") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Metric | Value |") | Out-Null
$markdown.Add("| --- | ---: |") | Out-Null
$markdown.Add("| Markdown files scanned | $($summary.scannedMarkdownFiles) |") | Out-Null
$markdown.Add("| Total findings | $($summary.totalFindings) |") | Out-Null
$markdown.Add("| Encoding findings | $($summary.encodingFindings) |") | Out-Null
$markdown.Add("| Claim-scope review findings | $($summary.reviewFindings) |") | Out-Null
$markdown.Add("| Canonical findings | $($summary.canonicalFindings) |") | Out-Null
$markdown.Add("| Canonical encoding findings | $($summary.canonicalEncodingFindings) |") | Out-Null
$markdown.Add("| Canonical defect findings | $($summary.canonicalDefectFindings) |") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("## Findings by classification") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Classification | Findings |") | Out-Null
$markdown.Add("| --- | ---: |") | Out-Null
foreach ($group in $classificationSummary) {
    $markdown.Add("| $($group.classification) | $($group.count) |") | Out-Null
}
$markdown.Add("") | Out-Null
$markdown.Add("## Findings by document scope") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| Document scope | Findings |") | Out-Null
$markdown.Add("| --- | ---: |") | Out-Null
foreach ($group in $scopeSummary) {
    $markdown.Add("| $($group.documentScope) | $($group.count) |") | Out-Null
}
$markdown.Add("") | Out-Null
$markdown.Add("## Top files by finding count") | Out-Null
$markdown.Add("") | Out-Null
$markdown.Add("| File | Findings |") | Out-Null
$markdown.Add("| --- | ---: |") | Out-Null
foreach ($group in $topFiles) {
    $markdown.Add("| $($group.Name) | $($group.Count) |") | Out-Null
}
$markdown.Add("") | Out-Null
$markdown.Add("## Notes") | Out-Null
$markdown.Add("") | Out-Null
foreach ($note in $summary.notes) {
    $markdown.Add("- $note") | Out-Null
}

Set-Content -LiteralPath $summaryMd -Value $markdown -Encoding UTF8

$correctedMarkdown = [System.Collections.Generic.List[string]]::new()
$correctedMarkdown.Add("# Corrected files") | Out-Null
$correctedMarkdown.Add("") | Out-Null
if ($normalizedCorrectedFiles.Count -eq 0) {
    $correctedMarkdown.Add("No corrected files were supplied to this auditor run. The auditor is read-only; pass -CorrectedFile to document a correction batch.") | Out-Null
}
else {
    $correctedMarkdown.Add("| File |") | Out-Null
    $correctedMarkdown.Add("| --- |") | Out-Null
    foreach ($fileName in ($normalizedCorrectedFiles | Sort-Object -Unique)) {
        $correctedMarkdown.Add("| $fileName |") | Out-Null
    }
}
Set-Content -LiteralPath $correctedFilesMd -Value $correctedMarkdown -Encoding UTF8

$remainingMarkdown = [System.Collections.Generic.List[string]]::new()
$remainingMarkdown.Add("# Remaining documentation review") | Out-Null
$remainingMarkdown.Add("") | Out-Null
$remainingMarkdown.Add("This file lists representative remaining findings after automatic classification. Historical evidence and planning records are not defects by default.") | Out-Null
$remainingMarkdown.Add("") | Out-Null
$remainingMarkdown.Add("## Canonical defects") | Out-Null
$remainingMarkdown.Add("") | Out-Null
$remainingMarkdown.Add("| File | Line | Rule | Text |") | Out-Null
$remainingMarkdown.Add("| --- | ---: | --- | --- |") | Out-Null
foreach ($finding in ($canonicalDefectFindings | Select-Object -First 100)) {
    $safeText = ($finding.Text -replace "\|", "\|")
    $remainingMarkdown.Add("| $($finding.File) | $($finding.Line) | $($finding.Rule) | $safeText |") | Out-Null
}
$remainingMarkdown.Add("") | Out-Null
$remainingMarkdown.Add("## Historical or informational findings") | Out-Null
$remainingMarkdown.Add("") | Out-Null
$markdownHistoricalFindings = $findings |
    Where-Object { $_.Classification -ne "DEFEITO_REAL" } |
    Select-Object -First 100
$remainingMarkdown.Add("| Classification | File | Line | Rule | Text |") | Out-Null
$remainingMarkdown.Add("| --- | --- | ---: | --- | --- |") | Out-Null
foreach ($finding in $markdownHistoricalFindings) {
    $safeText = ($finding.Text -replace "\|", "\|")
    $remainingMarkdown.Add("| $($finding.Classification) | $($finding.File) | $($finding.Line) | $($finding.Rule) | $safeText |") | Out-Null
}
Set-Content -LiteralPath $remainingReviewMd -Value $remainingMarkdown -Encoding UTF8

Write-Host "Documentation quality inventory exported to $resolvedOutputRoot"
Write-Host "Markdown files scanned: $($summary.scannedMarkdownFiles)"
Write-Host "Encoding findings: $($summary.encodingFindings)"
Write-Host "Claim-scope review findings: $($summary.reviewFindings)"
Write-Host "Canonical encoding findings: $($summary.canonicalEncodingFindings)"
Write-Host "Canonical defect findings: $($summary.canonicalDefectFindings)"

if ($FailOnMojibake -and $summary.encodingFindings -gt 0) {
    Write-Error "Documentation encoding findings detected: $($summary.encodingFindings)"
}

if ($FailOnCanonicalMojibake -and $summary.canonicalEncodingFindings -gt 0) {
    Write-Error "Canonical documentation encoding findings detected: $($summary.canonicalEncodingFindings)"
}

if ($FailOnCanonicalDefects -and $summary.canonicalDefectFindings -gt 0) {
    Write-Error "Canonical documentation defect findings detected: $($summary.canonicalDefectFindings)"
}
