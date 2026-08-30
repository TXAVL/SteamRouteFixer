# Build & Package Script for Steam Route Fixer
# Created by TXA Studio

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "🚀 BAT DAU DONG GOI STEAM ROUTE FIXER (TXA STUDIO)" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Stop any running instance
Write-Host "`n[1/5] Dong tien trinh SteamRouteFixer neu dang chay..." -ForegroundColor Gray
Stop-Process -Name "SteamRouteFixer" -Force -ErrorAction SilentlyContinue

# 2. Build Release Windows x64 Application
Write-Host "`n[2/5] Bien dich ma nguon ban Release .NET 10..." -ForegroundColor Gray
dotnet publish SteamRouteFixer.csproj -c Release -r win-x64 --self-contained false -o .\bin\Release\net10.0-windows\win-x64\publish\

# 3. Build Portable Single-File Binary
Write-Host "`n[3/5] Tao ban Portable Single-File Executable..." -ForegroundColor Gray
dotnet publish SteamRouteFixer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish_portable\

# 4. Compile Inno Setup Installer
Write-Host "`n[4/5] Bien dich trinh cai dat Setup bang Inno Setup..." -ForegroundColor Gray
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
}

if (Test-Path $isccPath) {
    & $isccPath "installer.iss"
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
    }
    Write-Host "Trinh cai dat da bien dich thanh cong!" -ForegroundColor Green
} else {
    Write-Warning "Khong tim thay ISCC.exe! Vui long kiem tra Inno Setup."
}

# 5. Consolidate into release_artifacts
Write-Host "`n[5/5] Tong hop cac goi phat hanh vao release_artifacts/..." -ForegroundColor Gray
if (-not (Test-Path ".\release_artifacts")) {
    New-Item -ItemType Directory -Path ".\release_artifacts" | Out-Null
}

Copy-Item ".\setup_output\SteamRouteFixer_Setup_v1.1.0.exe" ".\release_artifacts\" -Force -ErrorAction SilentlyContinue
Copy-Item ".\publish_portable\SteamRouteFixer.exe" ".\release_artifacts\SteamRouteFixer_Portable_v1.1.0.exe" -Force -ErrorAction SilentlyContinue

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "✅ DONG GOI HOAN TAT! Cac file Release da san sang:" -ForegroundColor Green
Get-ChildItem -Path ".\release_artifacts" | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host "==========================================================" -ForegroundColor Green
