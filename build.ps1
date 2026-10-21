$ErrorActionPreference = 'Stop'

Write-Host 'Restoring and running tests...' -ForegroundColor Cyan
dotnet restore .\tests\DropLink.Tests\DropLink.Tests.csproj
dotnet test .\tests\DropLink.Tests\DropLink.Tests.csproj -c Release

Write-Host 'Publishing self-contained Windows x64 app...' -ForegroundColor Cyan
dotnet publish .\src\DropLink\DropLink.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64

Write-Host 'Publish output: .\artifacts\win-x64\DropLink.exe' -ForegroundColor Green
