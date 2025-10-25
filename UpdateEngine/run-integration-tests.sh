#!/bin/bash

# Script to run integration tests with manual Azure Functions startup
# This provides an alternative to Aspire when the full Aspire workload isn't installed

echo "🚀 Starting Azure Functions Integration Test Runner"
echo "=================================================="

# Set up directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AZURE_FUNCTIONS_DIR="$SCRIPT_DIR/../azure-functions"
TEST_DIR="$SCRIPT_DIR/MicrosoftUpdateFunctions.Tests"

# Function to check if port is available
check_port() {
    if lsof -Pi :7071 -sTCP:LISTEN -t >/dev/null; then
        return 1  # Port is in use
    else
        return 0  # Port is available
    fi
}

# Function to wait for service to be ready
wait_for_service() {
    echo "⏳ Waiting for Azure Functions to be ready..."
    local max_attempts=30
    local attempt=1
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s http://localhost:7071/api/ClientWebService/ClientWebService.asmx > /dev/null 2>&1; then
            echo "✅ Azure Functions is ready!"
            return 0
        fi
        
        echo "   Attempt $attempt/$max_attempts - waiting..."
        sleep 2
        ((attempt++))
    done
    
    echo "❌ Azure Functions failed to start within timeout"
    return 1
}

# Cleanup function
cleanup() {
    echo "🧹 Cleaning up..."
    if [ ! -z "$FUNC_PID" ]; then
        echo "   Stopping Azure Functions (PID: $FUNC_PID)"
        kill $FUNC_PID 2>/dev/null
        wait $FUNC_PID 2>/dev/null
    fi
}

# Set up trap for cleanup
trap cleanup EXIT

# Check if port 7071 is already in use
if ! check_port; then
    echo "⚠️  Port 7071 is already in use. Please stop the existing service or choose a different port."
    exit 1
fi

echo "📂 Azure Functions directory: $AZURE_FUNCTIONS_DIR"
echo "📂 Test directory: $TEST_DIR"

# Start Azure Functions in background
echo "🔄 Starting Azure Functions..."
cd "$AZURE_FUNCTIONS_DIR"

# Start func in background and capture PID
func start --port 7071 > /tmp/func.log 2>&1 &
FUNC_PID=$!

echo "   Azure Functions PID: $FUNC_PID"
echo "   Log file: /tmp/func.log"

# Wait for the service to be ready
if ! wait_for_service; then
    echo "❌ Failed to start Azure Functions. Check log: /tmp/func.log"
    cat /tmp/func.log
    exit 1
fi

echo ""
echo "🧪 Running Integration Tests"
echo "============================"

# Change to test directory and run tests
cd "$TEST_DIR"

# Run all integration tests
echo "Running all integration tests..."
dotnet test --filter "FullyQualifiedName~Integration" --verbosity normal

TEST_RESULT=$?

echo ""
if [ $TEST_RESULT -eq 0 ]; then
    echo "✅ All integration tests passed!"
else
    echo "❌ Some integration tests failed (exit code: $TEST_RESULT)"
fi

echo ""
echo "🏁 Test run completed"
echo "📄 Azure Functions log available at: /tmp/func.log"

exit $TEST_RESULT