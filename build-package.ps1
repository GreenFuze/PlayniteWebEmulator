param(
    [Parameter(Mandatory = $true)]
    [string]$ToolboxPath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\PlayniteWebEmulator\PlayniteWebEmulator.csproj"
$buildOutput = Join-Path $repositoryRoot "src\PlayniteWebEmulator\bin\Release\net462"
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$stagingDirectory = Join-Path $artifactsDirectory "WebEmulator_0_1_0"
$toolboxPackage = Join-Path $artifactsDirectory "41d5bc40-a7e8-46a6-888e-d52cf719c397_0_1_0.pext"
$releasePackage = Join-Path $artifactsDirectory "WebEmulator_0_1_0.pext"
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifactsDirectory).TrimEnd('\') + '\'
$stagingFullPath = [System.IO.Path]::GetFullPath($stagingDirectory)

if (-not $stagingFullPath.StartsWith($artifactsFullPath, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Package staging must remain below the repository artifacts directory."
}

if (-not (Test-Path -LiteralPath $ToolboxPath -PathType Leaf))
{
    throw "Playnite Toolbox was not found at '$ToolboxPath'."
}

dotnet build $projectPath --configuration Release
if ($LASTEXITCODE -ne 0)
{
    throw "Release build failed."
}

if (Test-Path -LiteralPath $stagingDirectory)
{
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
foreach ($name in @(
    "PlayniteWebEmulator.dll",
    "PlayniteWebEmulator.Launcher.exe",
    "extension.yaml",
    "LICENSE",
    "NOTICE",
    "THIRD_PARTY_NOTICES.md",
    "USER_AGREEMENT.md"))
{
    $source = Join-Path $buildOutput $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf))
    {
        throw "Required package file was not produced: $source"
    }
    Copy-Item -LiteralPath $source -Destination $stagingDirectory
}

if (Test-Path -LiteralPath $toolboxPackage)
{
    Remove-Item -LiteralPath $toolboxPackage -Force
}

& $ToolboxPath pack $stagingDirectory $artifactsDirectory
if ($LASTEXITCODE -ne 0)
{
    throw "Playnite Toolbox packaging failed."
}

if (-not (Test-Path -LiteralPath $toolboxPackage -PathType Leaf))
{
    throw "Playnite Toolbox did not create the expected package."
}

Move-Item -LiteralPath $toolboxPackage -Destination $releasePackage -Force
Write-Host "Release package created at $releasePackage"
