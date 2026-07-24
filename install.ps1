# Screen Studio Clone — Portable Installer
# Creates a Start Menu shortcut + desktop shortcut for the self-contained app.
# Run after extracting ScreenStudio-win-x64.zip to a folder.
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\ScreenStudio",
    [switch]$DesktopShortcut
)

$ErrorActionPreference = "Stop"
$AppName = "Screen Studio"
$ExeName = "ScreenStudio.App.exe"

# Detect the script's directory (where the app files should be).
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceExe = Join-Path $ScriptDir $ExeName

if (-not (Test-Path $SourceExe)) {
    Write-Error "$ExeName not found in $ScriptDir. Run this script from the extracted zip folder."
    exit 1
}

Write-Host "Installing $AppName to $InstallDir..." -ForegroundColor Cyan

# Copy app files to install directory.
if ($InstallDir -ne $ScriptDir) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item -Path "$ScriptDir\*" -Destination $InstallDir -Recurse -Force
    Write-Host "  Copied files to $InstallDir"
}

$TargetExe = Join-Path $InstallDir $ExeName

# Create Start Menu shortcut.
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\$AppName"
New-Item -ItemType Directory -Force -Path $StartMenuDir | Out-Null
$ShortcutPath = Join-Path $StartMenuDir "$AppName.lnk"

$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $TargetExe
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "Professional screen recorder for Windows 11"
$Shortcut.IconLocation = $TargetExe
$Shortcut.Save()
Write-Host "  Created Start Menu shortcut: $ShortcutPath" -ForegroundColor Green

# Optional desktop shortcut.
if ($DesktopShortcut) {
    $DesktopPath = [Environment]::GetFolderPath("Desktop")
    $DesktopShortcutPath = Join-Path $DesktopPath "$AppName.lnk"
    $DesktopShortcut = $Shell.CreateShortcut($DesktopShortcutPath)
    $DesktopShortcut.TargetPath = $TargetExe
    $DesktopShortcut.WorkingDirectory = $InstallDir
    $DesktopShortcut.Save()
    Write-Host "  Created desktop shortcut: $DesktopShortcutPath" -ForegroundColor Green
}

# Register with Windows (Add/Remove Programs list).
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
New-Item -Path $RegPath -Force | Out-Null
Set-ItemProperty -Path $RegPath -Name "DisplayName" -Value $AppName
Set-ItemProperty -Path $RegPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $RegPath -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $RegPath -Name "DisplayIcon" -Value $TargetExe
Set-ItemProperty -Path $RegPath -Name "UninstallString" -Value "powershell -Command Remove-Item -Recurse -Force '$InstallDir'; Remove-Item '$ShortcutPath'"
Set-ItemProperty -Path $RegPath -Name "Publisher" -Value "Screen Studio Clone"
Set-ItemProperty -Path $RegPath -Name "NoModify" -Value 1
Set-ItemProperty -Path $RegPath -Name "NoRepair" -Value 1
Write-Host "  Registered in Add/Remove Programs" -ForegroundColor Green

Write-Host ""
Write-Host "$AppName installed successfully!" -ForegroundColor Cyan
Write-Host "  Find it in Start Menu, or run: $TargetExe"
Write-Host ""
Write-Host "To uninstall: Settings > Apps > $AppName > Uninstall"
