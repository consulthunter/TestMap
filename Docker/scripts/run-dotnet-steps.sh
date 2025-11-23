#!/bin/bash

# Arguments:
# $1 = run ID
# $2 = comma-separated solution file names to test
RUN_ID="$1"
SOL_NAMES="$2"

if [[ -z "$RUN_ID" || -z "$SOL_NAMES" ]]; then
    echo "Usage: $0 <run_id> <solution_name1.sln,solution_name2.sln,...>"
    exit 1
fi

CODE_DIR="/app/project/"
COV_DIR="/app/project/coverage/"

mkdir -p "$COV_DIR"
cd "$CODE_DIR" || { echo "Directory $CODE_DIR not found"; exit 1; }

IFS=',' read -ra names <<< "$SOL_NAMES"

for name in "${names[@]}"; do
    sln=$(find . -name "$name" | head -n 1)
    if [[ -z "$sln" ]]; then
        echo "Solution not found in container: $name"
        continue
    fi

    echo "Processing solution: $sln"
    TRX_FILE="${COV_DIR}/$(basename "$sln" .sln)_${RUN_ID}.trx"
    COVERAGE_FILE="${COV_DIR}/coverage_$(basename "$sln" .sln)_${RUN_ID}.cobertura.xml"
    
    dotnet add package Microsoft.CodeAnalysis.Metrics
    
    dotnet build -target:Metrics

    if ! dotnet test "$sln" \
        --collect:"Code Coverage;Format=Cobertura" \
        --logger "trx;LogFileName=$TRX_FILE" \
        --results-directory "$COV_DIR"; then
        echo "Testing failed for solution: $sln"
        continue
    fi

    if [[ -f "${COV_DIR}/coverage.cobertura.xml" ]]; then
        mv "${COV_DIR}/coverage.cobertura.xml" "$COVERAGE_FILE"
        echo "Coverage saved to: $COVERAGE_FILE"
    else
        echo "Coverage file not found for solution: $sln"
    fi
done

echo "All specified solutions processed."
