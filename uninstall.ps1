# Screen Studio Clone — Uninstaller
# Removes the app, shortcuts, and registry entries.
# Usage: powershell -ExecutionPolicy Bypass -File uninstall.ps1

param([string]$InstallDir = "$env:LOCALAPPDATA\ScreenStudio")

$ErrorActionPreference = "SilentlyContinue"
$AppName = "Wiggle"

Write-Host "Uninstalling $AppName..." -ForegroundColor Cyan

# Remove install directory.
if (Test-Path $InstallDir) {
    Remove-Item -Recurse -Force $InstallDir
    Write-Host "  Removed: $InstallDir"
}

# Remove Start Menu shortcut.
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\$AppName"
if (Test-Path $StartMenuDir) {
    Remove-Item -Recurse -Force $StartMenuDir
    Write-Host "  Removed Start Menu shortcuts"
}

# Remove desktop shortcut.
$DesktopPath = [Environment]::GetFolderPath("Desktop")
$DesktopShortcut = Join-Path $DesktopPath "$AppName.lnk"
if (Test-Path $DesktopShortcut) {
    Remove-Item -Force $DesktopShortcut
    Write-Host "  Removed desktop shortcut"
}

# Remove registry entry.
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
if (Test-Path $RegPath) {
    Remove-Item -Path $RegPath -Recurse
    Write-Host "  Removed registry entry"
}

Write-Host ""
Write-Host "$AppName uninstalled." -ForegroundColor Cyan
