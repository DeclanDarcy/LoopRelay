@echo off
setlocal

rem Publishes CommandCenter.CLI (Release, framework-dependent) so the loop runner can be
rem invoked from anywhere. Defaults to C:\tools\command-center; pass a path to override.
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
endlocal
