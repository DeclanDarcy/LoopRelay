@echo off
setlocal

rem Publishes CommandCenter.CLI (Release, framework-dependent) so the loop runner can be
rem invoked from anywhere, then publishes CommandCenter.Plan.CLI to its own default
rem directory (C:\tools\command-center-plan) via publish-plan-cli.bat.
rem Defaults CommandCenter.CLI's own output to C:\tools\command-center; pass a path to override.
rem Usage: publish-cli.bat [output-directory]

set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=C:\tools\command-center"

set "PROJECT=%~dp0src\CommandCenter.CLI\CommandCenter.CLI.csproj"

echo Publishing CommandCenter.CLI (Release) to "%OUTPUT_DIR%"...
dotnet publish "%PROJECT%" -c Release -o "%OUTPUT_DIR%" --nologo
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

echo.
echo Published "%OUTPUT_DIR%\CommandCenter.CLI.exe"

call "%~dp0publish-plan-cli.bat"
if errorlevel 1 (
    echo.
    echo Plan.CLI publish FAILED.
    exit /b 1
)

endlocal
