param (
    [string]$ContainerName,  # Docker container name
    [string]$LogsFilePath    # Path to the file where logs will be saved
)

Write-Host "Capturing logs from container '$ContainerName'..."

# Initialize variables for log capturing
$ContainerName = $ContainerName
$DockerLogsParentPath = "//wsl.localhost/docker-desktop/tmp/docker-desktop-root"
$DockerContainerLogsPath = (docker inspect --format '{{.LogPath}}' $ContainerName)
$ContainerLogsFullPath = $DockerLogsParentPath + $DockerContainerLogsPath
Write-Host "Logs Path is: $ContainerLogsFullPath"
Copy-Item -Path $ContainerLogsFullPath -Destination $LogsFilePath 


# Once logs are captured, remove the container
Write-Host "Logs captured. Removing container '$ContainerName'..."
docker rm $ContainerName


Write-Host "Container '$ContainerName' has been removed."
exit
