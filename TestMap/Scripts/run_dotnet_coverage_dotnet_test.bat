@echo off
dotnet-coverage collect -f xml dotnet test %1 --output F:\Projects\TestMap\TestMap\Validation\Coverage\%2_coverage.xml