[CmdletBinding()]
param(
    [string]$Version,
    [string]$PackageName,
    [switch]$Resume
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage ExitCode=$LASTEXITCODE"
    }
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Run this script from the v2rayN Pro repository."
}
Set-Location $repositoryRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$buildProps = Get-Content -LiteralPath (Join-Path $repositoryRoot "v2rayN/Directory.Build.props")
    $versionNode = $buildProps.SelectSingleNode("/Project/PropertyGroup/Version")
    if ($null -ne $versionNode) {
        $Version = $versionNode.InnerText
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Unable to resolve the application version."
}
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = "v2rayN-win-x64-$Version"
}

$artifactDir = Join-Path $repositoryRoot "artifacts"
$stageParent = Join-Path $artifactDir "$PackageName-stage"
$publishDir = Join-Path $stageParent "publish"
$coreParent = Join-Path $stageParent "core"
$coreArchive = Join-Path $stageParent "v2rayN-windows-64.zip"
$packageRoot = Join-Path $coreParent "v2rayN-windows-64"
$archive = Join-Path $artifactDir "$PackageName.zip"

if ((Test-Path -LiteralPath $stageParent) -and -not $Resume) {
    throw "Packaging target already exists: $stageParent or $archive"
}
if (Test-Path -LiteralPath $archive) {
    throw "Packaging archive already exists: $archive"
}

New-Item -ItemType Directory -Path $publishDir, $coreParent -Force | Out-Null

if (-not $Resume -or -not (Test-Path -LiteralPath (Join-Path $publishDir "v2rayN.exe"))) {
    Invoke-Checked `
        -File "dotnet" `
        -Arguments @(
            "publish", "v2rayN/v2rayN/v2rayN.csproj",
            "-c", "Release", "-r", "win-x64",
            "-p:SelfContained=true",
            "-p:EnableWindowsTargeting=true",
            "-p:Version=$Version",
            "-o", $publishDir
        ) `
        -FailureMessage "v2rayN publish failed."

    Invoke-Checked `
        -File "dotnet" `
        -Arguments @(
            "publish", "v2rayN/AmazTool/AmazTool.csproj",
            "-c", "Release", "-r", "win-x64",
            "-p:SelfContained=true",
            "-p:PublishTrimmed=true",
            "-p:EnableWindowsTargeting=true",
            "-p:Version=$Version",
            "-o", $publishDir
        ) `
        -FailureMessage "AmazTool publish failed."
}

Get-ChildItem -LiteralPath $publishDir -Recurse -Filter "*.pdb" | Remove-Item -Force
if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    if (-not (Test-Path -LiteralPath $coreArchive -PathType Leaf)) {
        Invoke-WebRequest `
            -Uri "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/v2rayN-windows-64.zip" `
            -OutFile $coreArchive
    }
    Expand-Archive -LiteralPath $coreArchive -DestinationPath $coreParent -Force
}

if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "Official runtime archive has an unexpected directory layout."
}
Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageRoot -Recurse -Force

$required = @(
    (Join-Path $packageRoot "v2rayN.exe"),
    (Join-Path $packageRoot "AmazTool.exe"),
    (Join-Path $packageRoot "bin/xray/xray.exe"),
    (Join-Path $packageRoot "bin/geosite.dat"),
    (Join-Path $packageRoot "bin/geoip.dat"),
    (Join-Path $packageRoot "bin/cfst/cfst.exe"),
    (Join-Path $packageRoot "bin/cfst/ip.txt"),
    (Join-Path $packageRoot "guiConfigs/default-workspace-background.gif")
)
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing required Windows runtime file: $file"
    }
}

if (Get-Command "7z" -ErrorAction SilentlyContinue) {
    Push-Location $coreParent
    try {
        Invoke-Checked `
            -File "7z" `
            -Arguments @("a", "-tZip", $archive, "v2rayN-windows-64", "-mx1") `
            -FailureMessage "7z packaging failed."
    }
    finally {
        Pop-Location
    }
}
else {
    Compress-Archive `
        -LiteralPath $packageRoot `
        -DestinationPath $archive `
        -CompressionLevel Fastest
}

$fileInfo = Get-Item -LiteralPath $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
Write-Host ""
Write-Host "Windows package created."
Write-Host "Archive: $archive"
Write-Host "Size: $($fileInfo.Length)"
Write-Host "SHA256: $hash"
