#!/bin/bash
# MechanicScope test runner.
#
# Compiles and RUNS the edit-mode test suite headlessly via the harness in Tools/HeadlessTests,
# which builds the real sources in Assets/Scripts/Core and the real tests in Assets/Tests/EditMode
# against a minimal UnityEngine shim. No Unity installation required.
#
# Exits non-zero if anything fails to compile or any test fails.

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HARNESS_DIR="$PROJECT_ROOT/Tools/HeadlessTests"

echo "=============================================="
echo "MechanicScope Test Runner"
echo "=============================================="
echo ""

if ! command -v dotnet > /dev/null 2>&1; then
    cat <<'EOF'
ERROR: the .NET SDK ('dotnet') was not found on PATH.

The headless harness needs the .NET 8 SDK to compile and run the tests:

  Ubuntu/Debian:  sudo apt-get install -y dotnet-sdk-8.0
  macOS:          brew install --cask dotnet-sdk
  Other:          https://dotnet.microsoft.com/download/dotnet/8.0

Alternatively, run the suite inside Unity:
  Window > General > Test Runner > EditMode > Run All
EOF
    exit 1
fi

echo "Test project: Tools/HeadlessTests"
echo "Sources:      Assets/Scripts/Core"
echo "Tests:        Assets/Tests/EditMode"
echo ""

# --nologo keeps output readable; verbosity is raised only for the test results themselves.
# '|| STATUS=$?' is required because 'set -e' would otherwise abort before the summary below.
STATUS=0
dotnet test "$HARNESS_DIR" --nologo --verbosity quiet "$@" || STATUS=$?

echo ""
if [ $STATUS -eq 0 ]; then
    echo "=============================================="
    echo "All tests passed."
    echo "=============================================="
else
    echo "=============================================="
    echo "TESTS FAILED (exit code $STATUS)"
    echo "=============================================="
fi

echo ""
echo "Note: this harness covers engine-independent logic (procedure sequencing, JSON"
echo "parsing, part lookup, shipped-data validation). Behaviour that genuinely depends"
echo "on Unity - rendering, AR, coroutine timing - must still be verified in the Editor:"
echo "  Window > General > Test Runner"

exit $STATUS
