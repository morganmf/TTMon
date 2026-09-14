<#
.SYNOPSIS
    Podpisuje TTMon.exe (lub dowolny plik) certyfikatem code-signing (samopodpisanym
    lub prawdziwym - dziala tak samo, o ile plik .pfx ma w sobie klucz prywatny).

.PARAMETER ExePath
    Sciezka do pliku do podpisania. Domyslnie: bin\Release\net8.0-windows\TTMon.exe

.PARAMETER CertPath
    Sciezka do pliku .pfx certyfikatu.

.PARAMETER CertPassword
    Haslo do .pfx. Jesli pominiesz, skrypt zapyta interaktywnie (bezpieczniej niz
    wpisywanie hasla w linii komend, gdzie zostaje w historii PowerShella).

.EXAMPLE
    .\scripts\sign-exe.ps1 -CertPath "C:\Certs\ttmon-selfsigned.pfx"
#>

param(
    [string]$ExePath = "bin\Release\net8.0-windows\TTMon.exe",
    [Parameter(Mandatory = $true)]
    [string]$CertPath,
    [securestring]$CertPassword
)

if (-not (Test-Path $ExePath)) {
    Write-Error "Nie znaleziono pliku do podpisania: $ExePath (zbuduj najpierw: dotnet build -c Release)"
    exit 1
}

if (-not (Test-Path $CertPath)) {
    Write-Error "Nie znaleziono certyfikatu: $CertPath"
    exit 1
}

if (-not $CertPassword) {
    $CertPassword = Read-Host "Haslo do certyfikatu (Enter jesli brak hasla)" -AsSecureString
}

# signtool.exe jest czescia Windows SDK - szukamy najnowszej wersji automatycznie
$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*x64*" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) {
    Write-Error @"
Nie znaleziono signtool.exe. To czesc Windows SDK - zainstaluj przez:
Visual Studio Installer -> Modyfikuj -> "Windows 10/11 SDK" (w zakladce
"Poszczegolne skladniki"), albo standalone: https://developer.microsoft.com/windows/downloads/windows-sdk/
"@
    exit 1
}

Write-Host "Uzywam signtool: $signtool"

$plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertPassword))

# /fd sha256 - algorytm skrotu podpisu (wspolczesny standard)
# /tr + /td sha256 - znacznik czasu (RFC 3161) - dzieki temu podpis zostaje
# wazny nawet po wygasnieciu certyfikatu, o ile byl wazny W MOMENCIE podpisania
& $signtool sign /f "$CertPath" /p "$plainPassword" /fd sha256 /tr http://timestamp.digicert.com /td sha256 "$ExePath"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Podpisano: $ExePath" -ForegroundColor Green
    & $signtool verify /pa "$ExePath"
} else {
    Write-Error "Podpisywanie nie powiodlo sie (kod $LASTEXITCODE)"
}
