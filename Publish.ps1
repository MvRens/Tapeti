param([switch]$nopush)


function pack
{
  param([string]$project)

  Write-Host "Packing $($project).csproj" -Foreground Blue
  NuGet.exe pack "$($project)\$($project).csproj" -Build -OutputDir publish -Version "$($version.NuGetVersion)" -Properties depversion="$($version.NuGetVersion)"
}


function push
{
  param([string]$project)

  Write-Host "Pushing $($project).csproj" -Foreground Blue
  NuGet.exe push "publish\X2Software.$($project).$($version.NuGetVersion).nupkg" -apikey "$($nugetkey)" -Source https://www.nuget.org/api/v2/package
}


$projects = @(
  "Tapeti.Annotations",
  "Tapeti",
  "Tapeti.DataAnnotations",
  "Tapeti.Flow",
  "Tapeti.SimpleInjector"
)


New-Item -Path publish -Type directory -Force | Out-Null

$version = GitVersion.exe | Out-String | ConvertFrom-Json
$nugetkey = Get-Content .nuget.apikey


Write-Host "Publishing version $($version.NuGetVersion) using API key $($nugetkey)"-Foreground Cyan

foreach ($project in $projects)
{
  pack($project)
}


if ($nopush -eq $false)
{
  foreach ($project in $projects)
  {
    push($project)
  }
}
else
{
  Write-Host "Skipping push" -Foreground Blue
}