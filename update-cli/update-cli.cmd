@echo off
REM UpdateEngine CLI launcher script for Windows

REM Get the directory where this script is located
set SCRIPT_DIR=%~dp0

REM Run the CLI application
dotnet run --project "%SCRIPT_DIR%update-cli.csproj" -- %*