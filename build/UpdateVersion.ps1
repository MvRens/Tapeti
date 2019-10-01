# For debugging purposes
if (-not (Test-Path env:APPVEYOR_BUILD_FOLDER))
{
    Write-Host "Warning: APPVEYOR_BUILD_FOLDER environment variable not set"
    $env:APPVEYOR_BUILD_FOLDER = "P:\Tapeti"
}

if (-not (Test-Path env:GitVersion_MajorMinorPatch))
{
    Write-Host "Warning: GitVersion_MajorMinorPatch environment variable not set"
    $env:GitVersion_MajorMinorPatch = "0.0.1"
}

if (-not (Test-Path env:GitVersion_CommitsSinceVersionSource))
{
    Write-Host "Warning: GitVersion_CommitsSinceVersionSource environment variable not set"
    $env:GitVersion_CommitsSinceVersionSource = "42"
}

$version = "$($env:GitVersion_MajorMinorPatch).$($env:GitVersion_CommitsSinceVersionSource)"

Write-Host "Updating version to $($version) for projects in $($env:APPVEYOR_BUILD_FOLDER)"

$projectFiles = Get-ChildItem $env:APPVEYOR_BUILD_FOLDER -Recurse *.csproj | Select -ExpandProperty FullName
foreach ($projectFile in $projectFiles)
{
    $contents = Get-Content -Path $projectFile
    if ($contents -match "<Version>(.+?)</Version>")
    {
        $contents = $contents -replace "<Version>(.+?)</Version>", "<Version>$($version)</Version>"
        Set-Content -Path $projectFile -Value $contents
        Write-Host "Updated $($projectFile)"
    }
    else
    {
        Write-Host "No version information in $($projectFile)"
    }
}