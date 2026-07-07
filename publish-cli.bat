@echo off
setlocal

rem Publishes LoopRelay.CLI (Release, framework-dependent) so the loop runner can be
rem invoked from anywhere, then publishes LoopRelay.Plan.CLI to its own default
rem directory (C:\tools\command-center-plan) via publish-plan-cli.bat.
rem Defaults LoopRelay.CLI's own output to C:\tools\command-center; pass a path to override.
rem Usage: publish-cli.bat [output-directory] [plan-output-directory]

set "OUTPUT_DIR=%~1"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=C:\tools\command-center"
set "PLAN_OUTPUT_DIR=%~2"

set "PROJECT=%~dp0src\LoopRelay.CLI\LoopRelay.CLI.csproj"
set "SETTINGS_TEMPLATE=%~dp0config\settings.default.json"
set "SETTINGS_FILE=%OUTPUT_DIR%\settings.json"

echo Publishing LoopRelay.CLI (Release) to "%OUTPUT_DIR%"...
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
echo Published "%OUTPUT_DIR%\LoopRelay.CLI.exe"

if "%PLAN_OUTPUT_DIR%"=="" (
    call "%~dp0publish-plan-cli.bat"
) else (
    call "%~dp0publish-plan-cli.bat" "%PLAN_OUTPUT_DIR%"
)
if errorlevel 1 (
    echo.
    echo Plan.CLI publish FAILED.
    exit /b 1
)

endlocal
