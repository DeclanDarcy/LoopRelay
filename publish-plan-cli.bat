@echo off
setlocal

rem Publishes LoopRelay.Plan.CLI (Release, framework-dependent) so the planning pipeline can be
rem invoked from anywhere. Defaults to C:\tools\command-center-plan; pass a path to override.
rem Usage: publish-plan-cli.bat [output-directory]

set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=C:\tools\command-center-plan"

set "PROJECT=%~dp0src\LoopRelay.Plan.CLI\LoopRelay.Plan.CLI.csproj"
set "SETTINGS_TEMPLATE=%~dp0config\settings.default.json"
set "SETTINGS_FILE=%OUTPUT_DIR%\settings.json"

echo Publishing LoopRelay.Plan.CLI (Release) to "%OUTPUT_DIR%"...
dotnet publish "%PROJECT%" -c Release -o "%OUTPUT_DIR%" --nologo
if errorlevel 1 (
    echo.
    echo Publish FAILED.
    exit /b 1
)

if not exist "%SETTINGS_FILE%" (
    copy "%SETTINGS_TEMPLATE%" "%SETTINGS_FILE%" >nul
    if errorlevel 1 (
        echo.
        echo Failed to create default settings file "%SETTINGS_FILE%".
        exit /b 1
    )
    echo Created "%SETTINGS_FILE%"
) else (
    echo Preserved existing "%SETTINGS_FILE%"
)

echo.
echo Published "%OUTPUT_DIR%\LoopRelay.Plan.CLI.exe"
endlocal
