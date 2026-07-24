@echo off
REM Screen Studio Clone — publish script
REM Produces a self-contained distributable in ./publish/ (no .NET runtime required on target).
REM Workaround: dotnet publish doesn't copy compiled XAML (.xbf/.pri) for unpackaged WinUI apps,
REM so we copy them from the build output explicitly.

setlocal
set CONFIG=Release
set RID=win-x64
set OUT=publish

echo Building Screen Studio (%CONFIG% / %RID%)...
dotnet publish src\ScreenStudio.App\ScreenStudio.App.csproj -c %CONFIG% -r %RID% --self-contained true -p:Platform=x64 -o .\%OUT%
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)

REM Copy missing XAML-compiler outputs (dotnet publish bug for unpackaged WinUI).
set BUILD=src\ScreenStudio.App\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64
echo Copying compiled XAML artifacts...
copy /Y "%BUILD%\App.xbf" "%OUT%\" >nul 2>&1
copy /Y "%BUILD%\MainWindow.xbf" "%OUT%\" >nul 2>&1
copy /Y "%BUILD%\ScreenStudio.App.pri" "%OUT%\" >nul 2>&1
if not exist "%OUT%\Views" mkdir "%OUT%\Views"
copy /Y "%BUILD%\Views\*.xbf" "%OUT%\Views\" >nul 2>&1

echo.
echo Published to .\%OUT%\ (%OUT%\ScreenStudio.App.exe)
echo Creating zip...
powershell -NoProfile -Command "Compress-Archive -Path '%OUT%/*' -DestinationPath 'ScreenStudio-win-x64.zip' -Force"
echo Done: ScreenStudio-win-x64.zip
endlocal
