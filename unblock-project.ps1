# Run this script as Administrator if Windows Security keeps blocking the project.
# Right-click PowerShell -> Run as administrator, then:
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   & "C:\Users\Lubi\Desktop\EasyRent_Checking\unblock-project.ps1"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Unblocking files in: $projectRoot"

Get-ChildItem $projectRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
}

$pathsToExclude = @(
    $projectRoot,
    "${env:ProgramFiles}\Microsoft Visual Studio",
    "${env:ProgramFiles}\dotnet",
    "${env:LOCALAPPDATA}\Programs\Cursor"
)

foreach ($path in $pathsToExclude) {
    if (Test-Path $path) {
        try {
            Add-MpPreference -ExclusionPath $path -ErrorAction Stop
            Write-Host "Added Defender exclusion: $path"
        }
        catch {
            Write-Host "Could not add exclusion (run as Admin): $path"
        }
    }
}

Write-Host ""
Write-Host "Done. Next steps:"
Write-Host "1. Close Visual Studio / Cursor completely"
Write-Host "2. Re-open EasyRent_Checking.sln"
Write-Host "3. If still blocked, open Windows Security -> Protection history and Allow the blocked item"
