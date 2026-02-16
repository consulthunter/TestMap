param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [Parameter(Mandatory = $true)]
    [string]$SolNames
)

$ErrorActionPreference = "Stop"

$ScriptDir = "C:\app\scripts"

Write-Host "=============================="
Write-Host "   TestMap Execution Runner"
Write-Host "=============================="
Write-Host "Run ID: $RunId"
Write-Host "Solutions: $SolNames"
Write-Host ""

# Track failures
$FailedComponents = @()

function Run-Step {
    param(
        [string]$Name,
        [string]$ScriptPath
    )

    Write-Host "--------------------------------"
    Write-Host " Running: $Name"
    Write-Host "--------------------------------"

    try {
        & $ScriptPath $RunId $SolNames
        Write-Host "[OK] $Name completed successfully"
    }
    catch {
        Write-Host "[FAIL] $Name failed"
        $FailedComponents += $Name
    }

    Write-Host ""
}

# Call each component
Run-Step "Unit Tests + Coverage" "$ScriptDir/run_dotnet_tests.ps1"
Run-Step "Mutation Testing (Stryker)" "$ScriptDir/run_dotnet_stryker.ps1"
Run-Step "Code Complexity (Lizard)" "$ScriptDir/run_lizard.ps1"

Write-Host "=============================="
Write-Host "           Summary"
Write-Host "=============================="

if ($FailedComponents.Count -eq 0) {
    Write-Host "All analysis steps completed successfully."
}
else {
    Write-Host "The following components failed:"
    foreach ($c in $FailedComponents) {
        Write-Host " - $c"
    }
}

Write-Host "Done."