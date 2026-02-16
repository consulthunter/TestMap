#!/bin/bash

RUN_ID="$1"

if [[ -z "$RUN_ID" ]]; then
    echo "Usage: $0 <run_id>"
    exit 1
fi

PROJECT_DIR="/app/project"
OUT_DIR="/app/project/lizard"

mkdir -p "$OUT_DIR"
cd "$PROJECT_DIR" || { echo "Project directory not found"; exit 1; }

OUTPUT_FILE="${OUT_DIR}/lizard_${RUN_ID}.xml"

echo "=== Running Lizard Complexity Analysis ==="

# Activate venv and run lizard
source /opt/lizardenv/bin/activate
lizard -x "**/bin/**" -x "**/obj/**" -x "**/packages/**" -x "**/node_modules/**" -X "$PROJECT_DIR" > "$OUTPUT_FILE"

if [[ $? -eq 0 ]]; then
    echo "Lizard XML report saved: $OUTPUT_FILE"
else
    echo "Lizard analysis failed"
fi

echo "=== Lizard Analysis Complete ==="
