@echo off
cd %1
dotnet stryker -r "json" --output F:\Projects\TestMap\TestMap\Validation\StrykerOutput\%2_report.json --msbuild-path %3