@echo off
setlocal

rem Publishes CommandCenter.Plan.CLI (Release, framework-dependent) so the planning pipeline can be
rem invoked from anywhere. Defaults to C:\tools\command-center-plan; pass a path to override.
rem Usage: publish-plan-cli.bat [output-directory]

set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=C:\tools\command-center-plan"

set "PROJECT=%~dp0src\CommandCenter.Plan.CLI\CommandCenter.Plan.CLI.csproj"

echo Publishing CommandCenter.Plan.CLI (Release) to "%OUTPUT_DIR%"...
dotnet publish "%PROJECT%" -c Release -o "%OUTPUT_DIR%" --nologo
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

echo.
echo Published "%OUTPUT_DIR%\CommandCenter.Plan.CLI.exe"
endlocal
