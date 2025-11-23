#!/bin/bash

# Arguments:
# $1 = run ID
# $2 = comma-separated .sln files
RUN_ID="$1"
SOL_NAMES="$2"

if [[ -z "$RUN_ID" || -z "$SOL_NAMES" ]]; then
    echo "Usage: $0 <run_id> <solution1.sln,solution2.sln>"
    exit 1
fi

CODE_DIR="/app/project"
OUT_DIR="/app/project/mutation"

mkdir -p "$OUT_DIR"
cd "$CODE_DIR" || exit 1

IFS=',' read -ra SOL_ARRAY <<< "$SOL_NAMES"

echo "=== Running Mutation Tests (dotnet-stryker) ==="

for sol in "${SOL_ARRAY[@]}"; do
    sln=$(find . -name "$sol" | head -n 1)

    if [[ -z "$sln" ]]; then
        echo "❌ Solution not found: $sol"
        continue
    fi

    sln_name=$(basename "$sln" .sln)
    sol_out="${OUT_DIR}/${sln_name}_${RUN_ID}"
    mkdir -p "$sol_out"

    echo "▶ Running Stryker for solution: $sln"

    dotnet stryker --solution "$sln" -r html -r markdown -r json --output "$sol_out"

    if [[ $? -eq 0 ]]; then
        echo "Reports saved in: $sol_out"
    else
        echo "Stryker failed for: $sln"
    fi
done

echo "=== Mutation Testing Complete ==="
