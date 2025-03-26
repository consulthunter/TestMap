param (
    [string]$LocalDir,      # Local directory to mount
    [string]$OutputDir,     # Directory to copy the results
    [string]$ImageName,     # Docker image name
    [string]$ContainerName  # Docker container name
)

# Validate that all required arguments are provided
if (-not $LocalDir -or -not $ImageName -or -not $ContainerName) {
    Write-Host "Usage: .\run_docker.ps1 -LocalDir '<path>' -OutputDir '<path>' -ImageName '<image>' -ContainerName '<name>'" -ForegroundColor Yellow
    exit
}

# Validate that the local directory exists
if (-not (Test-Path -Path $LocalDir)) {
    Write-Host "Error: The directory '$LocalDir' does not exist." -ForegroundColor Red
    exit
}

# Validate or create the output directory
if (-not (Test-Path -Path $OutputDir)) {
    Write-Host "Creating output directory: $OutputDir"
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Run the Docker container in detached mode (background)
Write-Host "Creating a container to run 'dotnet test' from image '$ImageName', mounting '$LocalDir', and naming it '$ContainerName'..."
docker run -d --name $ContainerName -v "${LocalDir}:/app/project" $ImageName /bin/bash ./scripts/run-dotnet-steps.sh


$containerRunning = $true
while ($containerRunning) {
    $status = docker inspect --format='{{.State.Status}}' $ContainerName
    if ($status -eq "exited") {
        $containerRunning = $false
        
    }
    Start-Sleep -Seconds 2
}

# Run dotnet coverage global (merge the reports)
Write-Host "Merging coverage reports..."

# Store the current location
Push-Location

# Change to the target directory
Set-Location -Path "$LocalDir\coverage"

# Define the command as a string
$command = 'dotnet-coverage merge **.cobertura.xml --output merged.cobertura.xml --output-format cobertura'

# Execute the command
Invoke-Expression $command

Pop-Location

# Copy the coverage file to the output directory
Write-Host "Copying coverage file from '$LocalDir\coverage\merged.cobertura.xml'..."
Copy-Item -Path "$LocalDir\coverage\merged.cobertura.xml" -Destination (Join-Path -Path $OutputDir -ChildPath "merged.cobertura.xml") -Force

Write-Host "'dotnet test' has completed. The container '$ContainerName' has been removed."
exit