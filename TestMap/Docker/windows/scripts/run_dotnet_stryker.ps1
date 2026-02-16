param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [string]$SolNames
)

$ErrorActionPreference = "Stop"

$CodeDir = "C:\app\project"
$OutDir  = "C:\app\project\mutation"

# Ensure output directory exists
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

# Validate project directory
if (-not (Test-Path $CodeDir)) {
    Write-Host "Project directory not found: $CodeDir"
    exit 1
}

Set-Location $CodeDir

# Split comma-separated solution names
$SolArray = $SolNames -split ','

Write-Host "=== Running Mutation Tests (dotnet-stryker) ==="

foreach ($sol in $SolArray) {

    # Find the solution file
    $sln = Get-ChildItem -Recurse -Filter $sol | Select-Object -First 1

    if (-not $sln) {
        Write-Host "Solution not found: $sol"
        continue
    }

    $slnName = [System.IO.Path]::GetFileNameWithoutExtension($sln.Name)
    $solOut  = Join-Path $OutDir "${slnName}_$RunId"

    if (-not (Test-Path $solOut)) {
        New-Item -ItemType Directory -Path $solOut | Out-Null
    }

    Write-Host "▶ Running Stryker for solution: $($sln.FullName)"

    try {
        dotnet stryker `
            --solution $sln.FullName `
            -r html `
            -r markdown `
            -r json `
            --output $solOut

        Write-Host "Reports saved in: $solOut"
    }
    catch {
        Write-Host "Stryker failed for: $($sln.FullName)"
    }
}

Write-Host "=== Mutation Testing Complete ==="