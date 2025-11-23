#!/bin/bash
set -euo pipefail

# Arguments:
# $1 = run ID
# $2 = comma-separated solution names (e.g. "MyApp.sln,Core.sln")

RUN_ID="$1"
SOL_NAMES="$2"

if [[ -z "$RUN_ID" || -z "$SOL_NAMES" ]]; then
    echo "Usage: $0 <run_id> <solution1.sln,solution2.sln,...>"
    exit 1
fi

SCRIPT_DIR="/app/scripts"

echo "=============================="
echo "   TestMap Execution Runner"
echo "=============================="
echo "Run ID: $RUN_ID"
echo "Solutions: $SOL_NAMES"
echo

# Track failures
FAILED_COMPONENTS=()

run_step() {
    local name="$1"
    local script="$2"

    echo "--------------------------------"
    echo " Running: $name"
    echo "--------------------------------"

    if "$script" "$RUN_ID" "$SOL_NAMES"; then
        echo "[OK] $name completed successfully"
    else
        echo "[FAIL] $name failed"
        FAILED_COMPONENTS+=("$name")
        # continue
    fi

    echo
}

# Call each component
run_step "Unit Tests + Coverage" "$SCRIPT_DIR/run_dotnet_tests.sh"
run_step "Mutation Testing (Stryker)" "$SCRIPT_DIR/run_dotnet_stryker.sh"
run_step "Code Complexity (Lizard)" "$SCRIPT_DIR/run_lizard.sh"

echo "=============================="
echo "           Summary"
echo "=============================="

if [[ ${#FAILED_COMPONENTS[@]} -eq 0 ]]; then
    echo "All analysis steps completed successfully."
else
    echo "The following components failed:"
    for c in "${FAILED_COMPONENTS[@]}"; do
        echo " - $c"
    done
fi

echo "Done."
