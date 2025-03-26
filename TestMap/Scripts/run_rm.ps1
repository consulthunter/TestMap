param(
    [string]$Path
)

if (Test-Path -Path $Path) {
    Remove-Item -Path $Path -Recurse -Force
    Write-Output "Directory '$Path' and its contents have been removed."
} else {
    Write-Output "Directory '$Path' does not exist."
}