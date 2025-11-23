# Docker

This application leverages Docker to automatically test repos that are cloned.

## Build the Images

TODO: Add script to build them

## Run the Image

TestMap leverages internal project information to decide which image to use.

It mounts the image to the temporary project folder and saves the results in that directory.

Afterwards, the container is removed.

### Example

```shell
docker run --rm -v .\:/app testing-net-9.0 dotnet-coverage collect dotnet test --output testing.coverage.xml --output-format cobertura
```