param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SingleFile,
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectPath = Join-Path $repoRoot "src\OpenAudio\OpenAudio.csproj"
$solutionPath = Join-Path $repoRoot "OpenAudio.sln"
$publishRoot = Join-Path $repoRoot "artifacts\publish"
$usingDefaultPublishDir = [string]::IsNullOrWhiteSpace($OutputDir)

if ($usingDefaultPublishDir) {
    $publishDir = Join-Path $publishRoot $Runtime
}
elseif ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $publishDir = $OutputDir
}
else {
    $publishDir = Join-Path $repoRoot $OutputDir
}

function Get-DotNetPath {
    $dotnetRootCandidate = $null
    if ($env:DOTNET_ROOT) {
        $dotnetRootCandidate = Join-Path $env:DOTNET_ROOT "dotnet.exe"
    }

    $candidates = @(
        (Join-Path $repoRoot ".dotnet\dotnet.exe"),
        (Join-Path $repoRoot ".dotnet-sdk\dotnet.exe"),
        (Join-Path (Split-Path -Parent $repoRoot) ".dotnet-sdk\dotnet.exe"),
        $dotnetRootCandidate,
        (Join-Path $env:ProgramFiles "dotnet\dotnet.exe")
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return $null
}

function Get-MSBuildPath {
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

if ($usingDefaultPublishDir) {
    New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

    Get-ChildItem -Path $publishRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "$Runtime-*" } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $publishDir) {
    Get-ChildItem -Path $publishDir -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$publishSingleFileValue = if ($SingleFile) { "true" } else { "false" }
$nativeSelfExtractValue = if ($SingleFile) { "true" } else { "false" }
$compressionValue = if ($SingleFile) { "true" } else { "false" }

$dotnet = Get-DotNetPath
if ($null -ne $dotnet) {
    & $dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: dotnet restore $solutionPath"
    }

    & $dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=$publishSingleFileValue `
        -p:IncludeNativeLibrariesForSelfExtract=$nativeSelfExtractValue `
        -p:EnableCompressionInSingleFile=$compressionValue `
        -p:PublishReadyToRun=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: dotnet publish $projectPath"
    }

    Write-Host "Publish completed to $publishDir"
    exit 0
}

$msbuild = Get-MSBuildPath
if ($null -ne $msbuild) {
    & $msbuild $projectPath `
        /restore `
        /t:Publish `
        /p:Configuration=$Configuration `
        /p:RuntimeIdentifier=$Runtime `
        /p:SelfContained=true `
        /p:PublishSingleFile=$publishSingleFileValue `
        /p:IncludeNativeLibrariesForSelfExtract=$nativeSelfExtractValue `
        /p:EnableCompressionInSingleFile=$compressionValue `
        /p:PublishReadyToRun=false `
        /p:PublishDir="$publishDir\"
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: MSBuild publish $projectPath"
    }

    Write-Host "Publish completed to $publishDir"
    exit 0
}

throw ".NET 8 SDK or Visual Studio 2022 MSBuild is required to publish this app."

