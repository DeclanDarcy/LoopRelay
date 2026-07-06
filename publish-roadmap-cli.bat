@echo off
setlocal

rem Publishes CommandCenter.Roadmap.CLI (Release, framework-dependent) so the roadmap workflow can be
rem invoked from anywhere. Defaults to C:\tools\command-center-roadmap; pass a path to override.
rem Usage: publish-roadmap-cli.bat [output-directory]

set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=C:\tools\command-center-roadmap"

set "PROJECT=%~dp0src\CommandCenter.Roadmap.CLI\CommandCenter.Roadmap.CLI.csproj"

echo Publishing CommandCenter.Roadmap.CLI (Release) to "%OUTPUT_DIR%"...
dotnet publish "%PROJECT%" -c Release -o "%OUTPUT_DIR%" --nologo
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

echo.
echo Published "%OUTPUT_DIR%\CommandCenter.Roadmap.CLI.exe"
endlocal
