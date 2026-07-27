# EasyRent_Checking - Fix "Application Control policy has blocked this file" (0x800711C7)
#
# RUN AS ADMINISTRATOR:
#   1. Right-click PowerShell -> Run as administrator
#   2. Set-ExecutionPolicy -Scope Process Bypass -Force
#   3. & "C:\Users\Lubi\Desktop\EasyRent_Checking\fix-windows-security-block.ps1"

$ErrorActionPreference = "Continue"
$projectRoots = @(
    "C:\Users\Lubi\Desktop\EasyRent_Checking",
    "C:\Dev\EasyRent_Checking"
)

Write-Host "=== Step 1: Unblock all project files ===" -ForegroundColor Cyan
foreach ($projectRoot in $projectRoots) {
    if (-not (Test-Path $projectRoot)) {
        continue
    }

    Write-Host "  $projectRoot"
    Get-ChildItem $projectRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
    }
}
Write-Host "Done."

Write-Host ""
Write-Host "=== Step 2: Add Windows Defender exclusions ===" -ForegroundColor Cyan
$exclusions = $projectRoots + @(
    "$env:ProgramFiles\dotnet",
    "$env:ProgramFiles\Microsoft Visual Studio",
    "$env:ProgramFiles(x86)\Microsoft Visual Studio",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio",
    "$env:LOCALAPPDATA\Programs\Cursor"
)

foreach ($path in $exclusions) {
    if (Test-Path $path) {
        try {
            Add-MpPreference -ExclusionPath $path -ErrorAction Stop
            Write-Host "  Added: $path" -ForegroundColor Green
        }
        catch {
            Write-Host "  Skipped: $path ($($_.Exception.Message))" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "=== Step 3: Clean and rebuild ===" -ForegroundColor Cyan
$primaryProject = $projectRoots | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($primaryProject) {
    Remove-Item "$primaryProject\bin" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$primaryProject\obj" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$primaryProject\.vs" -Recurse -Force -ErrorAction SilentlyContinue
    Set-Location $primaryProject
    dotnet build "$primaryProject\EasyRent_Checking.csproj"
}

Write-Host ""
Write-Host "=== If the app STILL will not run ===" -ForegroundColor Yellow
Write-Host @"

Windows Smart App Control is blocking your compiled app (error 0x800711C7).
Do ONE of the following:

A) Allow from Protection history (recommended first):
   1. Open Windows Security
   2. Protection history
   3. Find a blocked item for EasyRent_Checking or dotnet
   4. Click Actions -> Allow

B) Turn off Smart App Control:
   1. Settings -> Privacy & security -> Windows Security
   2. App & browser control -> Smart App Control settings
   3. Turn Off (cannot be turned on again easily after off)

C) Move project out of Desktop (often fixes it):
   Copy folder to: C:\Dev\EasyRent_Checking
   Open the .sln from the new location

D) Open the project from C:\Dev\EasyRent_Checking instead of Desktop
   (Smart App Control often blocks apps built under Desktop)

E) After allowing in Protection history, run:
   dotnet run --project "$primaryProject\EasyRent_Checking.csproj"

"@
