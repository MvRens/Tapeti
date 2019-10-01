# For debugging purposes
if (-not (Test-Path env:APPVEYOR_BUILD_FOLDER))
{
    Write-Host "Warning: APPVEYOR_BUILD_FOLDER environment variable not set"
    $env:APPVEYOR_BUILD_FOLDER = "P:\Tapeti"
}

if (-not (Test-Path env:GitVersion_AssemblySemVer))
{
    Write-Host "Warning: GitVersion_AssemblySemVer environment variable not set"
    $env:GitVersion_AssemblySemVer = "2.0.0"
}

Write-Host "Updating version to $($env:GitVersion_AssemblySemVer) for projects in $($env:APPVEYOR_BUILD_FOLDER)"

$projectFiles = Get-ChildItem $env:APPVEYOR_BUILD_FOLDER -Recurse *.csproj | Select -ExpandProperty FullName
foreach ($projectFile in $projectFiles)
{
    $contents = Get-Content -Path $projectFile
    if ($contents -match "<Version>(.+?)</Version>")
    {
        $contents = $contents -replace "<Version>(.+?)</Version>", "<Version>$($env:GitVersion_AssemblySemVer)</Version>"
        Set-Content -Path $projectFile -Value $contents
        Write-Host "Updated $($projectFile)"
    }
    else
    {
        Write-Host "No version information in $($projectFile)"
    }
}