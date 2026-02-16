param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [string]$SolNames
)

$ErrorActionPreference = "Stop"

$CodeDir = "C:\app\project"
$CovDir  = "C:\app\project\coverage"

# Ensure coverage directory exists
if (-not (Test-Path $CovDir)) {
    New-Item -ItemType Directory -Path $CovDir | Out-Null
}

# Validate project directory
if (-not (Test-Path $CodeDir)) {
    Write-Host "Directory $CodeDir not found"
    exit 1
}

Set-Location $CodeDir

# Split comma-separated solution names
$Names = $SolNames -split ','

foreach ($name in $Names) {

    # Find the solution file (first match)
    $sln = Get-ChildItem -Recurse -Filter $name | Select-Object -First 1

    if (-not $sln) {
        Write-Host "Solution not found in container: $name"
        continue
    }

    Write-Host "Processing solution: $($sln.FullName)"

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($sln.Name)
    $trxFile  = Join-Path $CovDir "$baseName`_$RunId.trx"

    try {
        dotnet test $sln.FullName `
            --collect:"XPlat Code Coverage" `
            --logger "trx;LogFileName=$trxFile" `
            --results-directory $CovDir
    }
    catch {
        Write-Host "Testing failed for solution: $($sln.FullName)"
        continue
    }
}

# Deduplicate Cobertura files
$coverageFiles = Get-ChildItem -Recurse -Path $CovDir -Filter "*.cobertura.xml"

$seen = @{}
$uniqueFiles = @()

foreach ($file in $coverageFiles) {
    $base = $file.Name
    if (-not $seen.ContainsKey($base)) {
        $uniqueFiles += $file.FullName
        $seen[$base] = $true
    }
    else {
        Write-Host "Skipping duplicate coverage file: $($file.FullName)"
    }
}

# Merge coverage
if ($uniqueFiles.Count -gt 0) {
    $merged = Join-Path $CovDir "merged_$RunId.cobertura.xml"

    dotnet-coverage merge $uniqueFiles `
        --output $merged `
        --output-format cobertura

    Write-Host "Merged coverage saved to: $merged"
}
else {
    Write-Host "No coverage files found to merge."
}

Write-Host "All specified solutions processed."