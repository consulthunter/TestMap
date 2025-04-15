# Get all subdirectories containing a Dockerfile
Get-ChildItem -Recurse -Filter "Dockerfile" | ForEach-Object {
    # Get the directory containing the Dockerfile
    $dir = $_.DirectoryName

    # Create an image name from the directory name: lowercase and hyphen-separated
    $imagename = ($dir.Split('\')[-1] -replace '\s|_|\.','-').ToLower()

    # Build the Docker image using the current directory as the build context
    Write-Host "Building Docker image: $imagename with context $PWD and Dockerfile in $dir"
    docker build -f "$dir\Dockerfile" -t $imagename .
}
