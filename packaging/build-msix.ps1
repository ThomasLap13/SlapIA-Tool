<#
.SYNOPSIS
    Builds an installable MSIX package for SlapIA Tool.

.DESCRIPTION
    1. Publishes the WPF app (self-contained, win-x64).
    2. Copies the appx manifest + assets next to the published exe.
    3. Packs everything into a .msix with makeappx.exe (Windows SDK).
    4. Optionally signs the package with a local self-signed certificate so it
       can be sideloaded for testing. This is NOT the certificate you submit
       to the Microsoft Store - the Store re-signs the package itself.

.PARAMETER Sign
    If set, creates (or reuses) a self-signed certificate and signs the
    package so it can be installed locally without Developer Mode changes
    to trust settings beyond importing the certificate once.
#>
param(
    [switch]$Sign
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\SlapIA.App\SlapIA.App.csproj"
$publishDir = Join-Path $root "packaging\publish"
$msixOutDir = Join-Path $root "packaging\out"
$assetsDir = Join-Path $root "packaging\Assets"
$manifest = Join-Path $root "packaging\Package.appxmanifest"

$sdkBin = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory |
    Where-Object { $_.Name -match "^\d+\.\d+\.\d+\.\d+$" } |
    Sort-Object Name -Descending | Select-Object -First 1
$makeappx = Join-Path $sdkBin.FullName "x64\makeappx.exe"
$signtool = Join-Path $sdkBin.FullName "x64\signtool.exe"

if (-not (Test-Path $makeappx)) { throw "makeappx.exe introuvable. Installez le Windows SDK." }

Write-Host "==> Publication de l'application (self-contained win-x64)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish a echoue." }

Write-Host "==> Copie du manifeste et des assets..." -ForegroundColor Cyan
Copy-Item $manifest (Join-Path $publishDir "AppxManifest.xml") -Force
Copy-Item $assetsDir (Join-Path $publishDir "Assets") -Recurse -Force

New-Item -ItemType Directory -Force -Path $msixOutDir | Out-Null
$msixPath = Join-Path $msixOutDir "SlapIA.Tool.msix"
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }

Write-Host "==> Empaquetage MSIX..." -ForegroundColor Cyan
& $makeappx pack /d $publishDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx a echoue." }

if ($Sign) {
    Write-Host "==> Signature locale (test / sideload uniquement)..." -ForegroundColor Cyan
    $certSubject = "CN=SlapIA"
    $pfxPath = Join-Path $msixOutDir "SlapIA-dev-cert.pfx"
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $certSubject `
            -KeyUsage DigitalSignature -FriendlyName "SlapIA Tool Dev Cert" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    }
    $pwd = ConvertTo-SecureString -String "slapia" -Force -AsPlainText
    Export-PfxCertificate -Cert ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -FilePath $pfxPath -Password $pwd | Out-Null
    & $signtool sign /fd SHA256 /a /f $pfxPath /p "slapia" $msixPath
    if ($LASTEXITCODE -ne 0) { throw "signtool a echoue." }
    Write-Host ""
    Write-Host "Pour installer ce paquet signe localement, importez d'abord le certificat" -ForegroundColor Yellow
    Write-Host "dans 'Autorites de certification racines de confiance' :" -ForegroundColor Yellow
    Write-Host "  Import-Certificate -FilePath '$pfxPath' -CertStoreLocation Cert:\LocalMachine\Root" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Paquet cree : $msixPath" -ForegroundColor Green
