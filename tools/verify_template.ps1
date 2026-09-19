[CmdletBinding()]
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repository = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$verificationRoot = Join-Path ([IO.Path]::GetTempPath()) ("sunmao-template-" + [Guid]::NewGuid().ToString("N"))
$packageRoot = Join-Path $verificationRoot "packages"
$generatedRoot = Join-Path $verificationRoot "generated"
$hiveRoot = Join-Path $verificationRoot "template-hive"
$nugetCache = Join-Path $verificationRoot "nuget-cache"
$restoreConfig = Join-Path $verificationRoot "NuGet.Config"
$frameworks = @("net8.0-windows", "net10.0-windows")
$versionMatch = Select-String -LiteralPath (Join-Path $repository "Directory.Build.props") -Pattern '<Version>([^<]+)</Version>' |
    Select-Object -First 1
if ($null -eq $versionMatch)
{
    throw "The repository package version was not found."
}

$packageVersion = $versionMatch.Matches[0].Groups[1].Value
$versionPattern = 'Version="' + [regex]::Escape($packageVersion) + '"'
$templateProjects = Get-ChildItem -LiteralPath (Join-Path $repository "templates\sunmao-app") -Filter "*.csproj" -File -Recurse
foreach ($templateProject in $templateProjects)
{
    $templateText = Get-Content -LiteralPath $templateProject.FullName -Raw
    if ($templateText -notmatch $versionPattern)
    {
        throw "Template package version in $($templateProject.FullName) does not match $packageVersion."
    }
}

New-Item -ItemType Directory -Path $packageRoot, $generatedRoot, $hiveRoot, $nugetCache | Out-Null
$escapedPackageRoot = [System.Security.SecurityElement]::Escape($packageRoot)
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedPackageRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $restoreConfig -Encoding utf8

function Invoke-Dotnet([string[]]$Arguments)
{
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-Dotnet @(
    "restore",
    (Join-Path $repository "Sunmao.slnx"),
    "--configfile",
    $restoreConfig,
    "-p:RestoreDisableParallel=true"
)

$projects = Get-ChildItem -LiteralPath (Join-Path $repository "src") -Filter "*.csproj" -File -Recurse |
    Sort-Object FullName
foreach ($project in $projects)
{
    Invoke-Dotnet @(
        "pack",
        $project.FullName,
        "--no-restore",
        "--configuration",
        $Configuration,
        "-m:1",
        "-p:PackageOutputPath=$packageRoot",
        "-p:UseSharedCompilation=false",
        "-p:PackageVersion=$packageVersion",
        "-v:minimal"
    )
}

if (-not (Get-ChildItem -LiteralPath $packageRoot -Filter "*.nupkg" -File -ErrorAction SilentlyContinue))
{
    throw "No library packages were produced in $packageRoot."
}

$templateRoot = Join-Path $repository "templates\sunmao-app"
Invoke-Dotnet @("new", "--debug:custom-hive", $hiveRoot, "install", $templateRoot)
$previousNugetPackages = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = $nugetCache
foreach ($framework in $frameworks)
{
    $frameworkRoot = Join-Path $generatedRoot $framework
    New-Item -ItemType Directory -Path $frameworkRoot | Out-Null

    Invoke-Dotnet @(
        "new",
        "--debug:custom-hive",
        $hiveRoot,
        "sunmao-app",
        "--name",
        "SampleApp",
        "--output",
        $frameworkRoot,
        "--framework",
        $framework
    )

    $testProject = Get-ChildItem -LiteralPath $frameworkRoot -Filter "*.Tests.csproj" -Recurse -File |
        Select-Object -First 1
    if ($null -eq $testProject)
    {
        throw "The generated test project was not found for $framework."
    }

    Invoke-Dotnet @(
        "restore",
        $testProject.FullName,
        "--configfile",
        $restoreConfig,
        "--ignore-failed-sources",
        "-p:RestoreDisableParallel=true"
    )

    Invoke-Dotnet @("build", $testProject.FullName, "--no-restore", "-m:1", "-p:UseSharedCompilation=false")
    Invoke-Dotnet @("test", $testProject.FullName, "--no-build", "--no-restore", "-m:1", "-p:UseSharedCompilation=false")

    Write-Output "Template verification passed for ${framework}: $($testProject.FullName)"
}

if ($null -eq $previousNugetPackages)
{
    Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
}
else
{
    $env:NUGET_PACKAGES = $previousNugetPackages
}

Remove-Item -LiteralPath $verificationRoot -Recurse -Force
