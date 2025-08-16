#!/bin/bash

# Directory in the container where solution files are located
CODE_DIR="/app/project/"
COV_DIR="/app/project/coverage/"

# Navigate to the mounted directory
cd "$CODE_DIR" || { echo "Directory $CODE_DIR not found"; exit 1; }

# Find all .sln files recursively
echo "Searching for solution files (.sln)..."
sln_files=$(find . -name "*.sln")

# Check if any solution files were found
if [[ -z "$sln_files" ]]; then
    echo "No solution files found in $CODE_DIR. Exiting."
    exit 1
fi

# Loop through each solution file and run the commands
for sln in $sln_files; do
    # Extract just the filename
    sln_filename=$(basename "$sln")
    echo "Processing solution: $sln"
    sln_dir=$(dirname "$sln")

    echo "Running dotnet test..."
    if ! dotnet test $sln --collect:"Code Coverage;Format=Cobertura" --logger "trx;LogFileName=$(basename "$sln" .sln)_results.trx" --results-directory $COV_DIR; then
      echo "Testing failed for solution: $sln. Skipping to the next one."
      continue
    fi

    # Return to the initial directory
done
echo "All solutions have been processed successfully!"
