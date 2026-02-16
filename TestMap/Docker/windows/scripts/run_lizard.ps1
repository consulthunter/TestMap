param(
    [Parameter(Mandatory = $true)]
    [string]$RunId
)

$ErrorActionPreference = "Stop"

$ProjectDir = "C:\app\project"
$OutDir     = "C:\app\project\lizard"

# Ensure output directory exists
if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

# Validate project directory
if (-not (Test-Path $ProjectDir)) {
    Write-Host "Project directory not found"
    exit 1
}

Set-Location $ProjectDir

$OutputFile = Join-Path $OutDir "lizard_$RunId.xml"

Write-Host "=== Running Lizard Complexity Analysis ==="

# Activate venv and run lizard
$VenvActivate = "C:\lizardenv\Scripts\Activate.ps1"

if (-not (Test-Path $VenvActivate)) {
    Write-Host "Python virtual environment not found at $VenvActivate"
    exit 1
}

# Activate venv
. $VenvActivate

# Run lizard
try {
    lizard `
        -x "**/bin/**" `
        -x "**/obj/**" `
        -x "**/packages/**" `
        -x "**/node_modules/**" `
        -X $ProjectDir `
        | Out-File -FilePath $OutputFile -Encoding utf8

    Write-Host "Lizard XML report saved: $OutputFile"
}
catch {
    Write-Host "Lizard analysis failed"
}

Write-Host "=== Lizard Analysis Complete ==="