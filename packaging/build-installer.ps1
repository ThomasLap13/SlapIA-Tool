<#
.SYNOPSIS
    Builds a classic Setup.exe installer for SlapIA Tool, with built-in auto-update support.

.DESCRIPTION
    1. Publishes the WPF app (self-contained, win-x64) so the target machine needs nothing
       pre-installed.
    2. Packs it with Velopack's `vpk` CLI into packaging/Releases/ :
         - Setup.exe            <- what you distribute / put in a GitHub Release
         - SlapIA.Tool-win-Portable.zip
         - *.nupkg + RELEASES   <- the update feed the app reads via GithubSource
    3. Setup.exe installs like any normal Windows app (Program Files, Start Menu shortcut,
       entry in "Applications installees", uninstaller) and registers the app so future
       runs can self-update from GitHub Releases.

.PARAMETER Version
    Version to stamp this release with (e.g. 0.2.0). Defaults to the <Version> in the csproj.

.NOTES
    After running this script, create a GitHub Release on
    https://github.com/ThomasLap13/SlapIA-Tool tagged "v<Version>" and upload every file
    produced under packaging/Releases/ as release assets. That's what the in-app
    "Verifier les mises a jour" button checks against.
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\SlapIA.App\SlapIA.App.csproj"
$helperProject = Join-Path $root "src\SlapIA.SensorHelper\SlapIA.SensorHelper.csproj"
$publishDir = Join-Path $root "packaging\publish"
$releasesDir = Join-Path $root "packaging\Releases"
$icon = Join-Path $root "packaging\Assets\app.ico"
$license = Join-Path $root "LICENSE"

if (-not $Version) {
    $csproj = Get-Content $appProject -Raw
    if ($csproj -match "<Version>([\d\.]+)</Version>") {
        $Version = $Matches[1]
    } else {
        throw "Impossible de determiner la version. Utilisez -Version 0.1.0"
    }
}
Write-Host "==> Version : $Version" -ForegroundColor Cyan

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    throw "L'outil 'vpk' est introuvable. Installez-le avec : dotnet tool install -g vpk"
}

Write-Host "==> Publication de l'application (self-contained win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a echoue." }

Write-Host "==> Publication du helper de capteurs (temperature CPU)..." -ForegroundColor Cyan
dotnet publish $helperProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (SensorHelper) a echoue." }

Write-Host "==> Empaquetage avec vpk (Velopack)..." -ForegroundColor Cyan
vpk pack `
    --packId "SlapIA.Tool" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "SlapIA.Tool.exe" `
    --packTitle "SlapIA Tool" `
    --packAuthors "Thomas Lapierre" `
    --icon $icon `
    --instLicense $license `
    --outputDir $releasesDir
if ($LASTEXITCODE -ne 0) { throw "vpk pack a echoue." }

Write-Host ""
Write-Host "Installeur cree dans : $releasesDir" -ForegroundColor Green
Write-Host "Prochaine etape : publiez tout le contenu de ce dossier sur une" -ForegroundColor Yellow
Write-Host "GitHub Release taguee 'v$Version' sur https://github.com/ThomasLap13/SlapIA-Tool/releases" -ForegroundColor Yellow
Write-Host "pour que le bouton 'Verifier les mises a jour' de l'application la detecte." -ForegroundColor Yellow
