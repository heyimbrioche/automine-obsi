# ══════════════════════════════════════════════════════════════
#  AutoMine Obsidienne - Build Release Securise
#  Usage : .\build-release.ps1
#  Resultat : src\AutoMinePactify\publish-secure\AutoMinePactify.exe
# ══════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$projectDir = Join-Path $rootDir "src\AutoMinePactify"
$csproj = Join-Path $projectDir "AutoMinePactify.csproj"
$publishOutput = Join-Path $projectDir "publish-secure"

Write-Host ""
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "  AutoMine - Build Release Securise" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""

# ── Etape 1 : Clean du dossier publish ──
Write-Host "[1/3] Nettoyage..." -ForegroundColor Cyan
if (Test-Path $publishOutput) {
    Remove-Item $publishOutput -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "  OK" -ForegroundColor Green

# ── Etape 2 : Publish single-file self-contained ──
Write-Host "[2/3] Build + Publish (win-x64 self-contained)..." -ForegroundColor Cyan

dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:DebugType=none `
    /p:DebugSymbols=false `
    -o $publishOutput --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERREUR : Publish echoue !" -ForegroundColor Red
    exit 1
}
Write-Host "  OK" -ForegroundColor Green

# ── Etape 3 : Nettoyage des .pdb ──
Write-Host "[3/3] Suppression des .pdb..." -ForegroundColor Cyan
Get-ChildItem $publishOutput -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Write-Host "  OK" -ForegroundColor Green

# ── Resultat ──
$exePath = Join-Path $publishOutput "AutoMinePactify.exe"
if (Test-Path $exePath) {
    $size = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  BUILD TERMINE !" -ForegroundColor Green
    Write-Host "  .exe : $exePath" -ForegroundColor Green
    Write-Host "  Taille : ${size} MB" -ForegroundColor Green
    Write-Host "  Protections :" -ForegroundColor Green
    Write-Host "    - Anti-debug (IsDebuggerPresent + remote)" -ForegroundColor Green
    Write-Host "    - Anti-dump (ThreadHideFromDebugger)" -ForegroundColor Green
    Write-Host "    - Verif periodique anti-debug (2 min)" -ForegroundColor Green
    Write-Host "    - Licence KeyAuth + cache AES" -ForegroundColor Green
    Write-Host "    - Revalidation licence (2h)" -ForegroundColor Green
    Write-Host "    - Pas de .pdb (pas de debug symbols)" -ForegroundColor Green
    Write-Host "    - Single-file (code embarque)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "ERREUR : .exe introuvable !" -ForegroundColor Red
    exit 1
}
