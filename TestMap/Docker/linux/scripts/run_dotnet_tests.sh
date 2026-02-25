#!/bin/bash

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

TRX_FILES=()

# 1️⃣ Run tests for each solution
for name in "${names[@]}"; do
    sln=$(find . -name "$name" | head -n 1)
    if [[ -z "$sln" ]]; then
        echo "Solution not found in container: $name"
        continue
    fi

    echo "Processing solution: $sln"

    if ! dotnet test "$sln" \
        --collect:"Code Coverage;Format=Cobertura" \
        --logger "trx" \
        --results-directory "$COV_DIR"; then
        echo "Testing failed for solution: $sln"
        continue
    fi
done

# 2️⃣ Collect cobertura coverage files
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

if [[ ${#COVERAGE_TO_MERGE[@]} -gt 0 ]]; then
    MERGED_RAW="${COV_DIR}merged_${RUN_ID}_raw.cobertura.xml"
    MERGED_NORMALIZED="${COV_DIR}merged_${RUN_ID}.cobertura.xml"
    REPORT_DIR="${COV_DIR}report_${RUN_ID}/"

    # 3️⃣ Merge coverage files (keep raw)
    dotnet-coverage merge "${COVERAGE_TO_MERGE[@]}" \
        --output "$MERGED_RAW" \
        --output-format cobertura
    echo "Merged raw coverage saved to: $MERGED_RAW"

    # 4️⃣ Run ReportGenerator to normalize names and produce final Cobertura + HTML
    reportgenerator \
        -reports:"$MERGED_RAW" \
        -targetdir:"$REPORT_DIR" \
        -reporttypes:Cobertura \
        -verbosity:Verbose

    echo "Normalized coverage saved to: ${REPORT_DIR}Cobertura.xml"
else
    echo "No coverage files found to merge."
fi

echo "All specified solutions processed."