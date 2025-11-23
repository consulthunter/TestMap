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

    if ! dotnet test "$sln" \
        --collect:"Code Coverage;Format=Cobertura" \
        --logger "trx;LogFileName=$TRX_FILE" \
        --results-directory "$COV_DIR"; then
        echo "Testing failed for solution: $sln"
        continue
    fi
done

# Find all cobertura files, keep only one per filename
declare -A seen_files
COVERAGE_TO_MERGE=()

while IFS= read -r file; do
    base=$(basename "$file")
    if [[ -z "${seen_files[$base]}" ]]; then
        COVERAGE_TO_MERGE+=("$file")
        seen_files[$base]=1
    else
        echo "Skipping duplicate coverage file: $file"
    fi
done < <(find "$COV_DIR" -type f -name "*.cobertura.xml")

# Merge only unique files
if [[ ${#COVERAGE_TO_MERGE[@]} -gt 0 ]]; then
    dotnet-coverage merge "${COVERAGE_TO_MERGE[@]}" \
        --output "${COV_DIR}merged_${RUN_ID}.cobertura.xml" \
        --output-format cobertura
    echo "Merged coverage saved to: ${COV_DIR}merged_${RUN_ID}.cobertura.xml"
else
    echo "No coverage files found to merge."
fi

echo "All specified solutions processed."
