[CmdletBinding()]
param(
    [ValidateSet("all", "tests", "windows", "macos")]
    [string]$Target = "all",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host ""
    Write-Host "== $Title =="
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Run this script from the v2rayN Pro Git repository."
}
Set-Location $repositoryRoot

Invoke-Checked -Title "Git whitespace validation" -File "git" -Arguments @("diff", "--check")

if ($Target -in @("all", "tests")) {
    Invoke-Checked `
        -Title "ServiceLib tests" `
        -File "dotnet" `
        -Arguments @("test", "--project", "v2rayN/ServiceLib.Tests", "-c", $Configuration)
}

if ($Target -in @("all", "windows")) {
    Invoke-Checked `
        -Title "Windows x64 build" `
        -File "dotnet" `
        -Arguments @(
            "build",
            "v2rayN/v2rayN/v2rayN.csproj",
            "-c",
            $Configuration,
            "-r",
            "win-x64"
        )
}

if ($Target -in @("all", "macos")) {
    Invoke-Checked `
        -Title "macOS ARM64 build" `
        -File "dotnet" `
        -Arguments @(
            "build",
            "v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj",
            "-c",
            $Configuration,
            "-r",
            "osx-arm64"
        )
}

Write-Host ""
Write-Host "All requested custom build checks passed."
