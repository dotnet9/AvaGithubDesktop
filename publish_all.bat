@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\AvaGithubDesktop\AvaGithubDesktop.csproj"
set "PACKAGE_AFTER=false"
set "WINDOWS_TFM=net11.0-windows"
set "CROSS_PLATFORM_TFM=net11.0"

if not "%~1"=="" (
    if /I "%~1"=="--package" (
        set "PACKAGE_AFTER=true"
    ) else (
        echo Usage: publish_all.bat [--package]
        exit /b 2
    )
)

echo Publishing AvaGithubDesktop profiles...

call :publish FolderProfile__win-x64 %WINDOWS_TFM% win-x64 || exit /b 1
call :publish FolderProfile__win-x86 %WINDOWS_TFM% win-x86 || exit /b 1
call :publish FolderProfile__linux-x64 %CROSS_PLATFORM_TFM% linux-x64 || exit /b 1
call :publish FolderProfile__linux-arm64 %CROSS_PLATFORM_TFM% linux-arm64 || exit /b 1
call :publish FolderProfile__osx-x64 %CROSS_PLATFORM_TFM% osx-x64 || exit /b 1
call :publish FolderProfile__osx-arm64 %CROSS_PLATFORM_TFM% osx-arm64 || exit /b 1

echo All AvaGithubDesktop publish profiles completed.

if /I "%PACKAGE_AFTER%"=="true" (
    echo.
    echo Packaging AvaGithubDesktop release artifacts...
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\package_ava_github_desktop_artifacts.ps1" || exit /b 1
)

exit /b 0

:publish
echo.
echo === %~1 ===
dotnet publish "%PROJECT%" -c Release -f %~2 -r %~3 /p:PublishProfile=%~1
exit /b %ERRORLEVEL%
