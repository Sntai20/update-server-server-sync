#!/bin/bash
# UpdateEngine CLI launcher script for Unix/Linux/macOS

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Run the CLI application
dotnet run --project "$SCRIPT_DIR/update-cli.csproj" -- "$@"