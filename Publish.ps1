param([switch]$nopush)

New-Item -Path publish -Type directory -Force | Out-Null

$version = GitVersion.exe | Out-String | ConvertFrom-Json
$nugetkey = Get-Content .nuget.apikey


Write-Host "Publishing version $($version.NuGetVersion) using API key $($nugetkey)"-Foreground Cyan


Write-Host "Packing Tapeti.Annotations.csproj" -Foreground Blue
NuGet.exe pack Tapeti.Annotations\Tapeti.Annotations.csproj -OutputDir publish -Version $version.NuGetVersion -Properties depversion="$($version.NuGetVersion)"

Write-Host "Packing Tapeti.csproj" -Foreground Blue
NuGet.exe pack Tapeti\Tapeti.csproj -OutputDir publish -Version $version.NuGetVersion -Properties depversion="$($version.NuGetVersion)"

Write-Host "Packing Tapeti.Flow.csproj" -Foreground Blue
NuGet.exe pack Tapeti.Flow\Tapeti.Flow.csproj -OutputDir publish -Version $version.NuGetVersion -Properties depversion="$($version.NuGetVersion)"

Write-Host "Packing Tapeti.SimpleInjector.csproj" -Foreground Blue
NuGet.exe pack Tapeti.SimpleInjector\Tapeti.SimpleInjector.csproj -OutputDir publish -Version $version.NuGetVersion -Properties depversion="$($version.NuGetVersion)"


if ($nopush -eq $false)
{
  Write-Host "Pushing Tapeti.Annotations.csproj" -Foreground Blue
  NuGet.exe push publish\X2Software.Tapeti.Annotations.$($version.NuGetVersion).nupkg -apikey $nugetkey -Source https://www.nuget.org/api/v2/package

  Write-Host "Pushing Tapeti.csproj" -Foreground Blue
  NuGet.exe push publish\X2Software.Tapeti.$($version.NuGetVersion).nupkg -apikey $nugetkey -Source https://www.nuget.org/api/v2/package

  Write-Host "Pushing Tapeti.Flow.csproj" -Foreground Blue
  NuGet.exe push publish\X2Software.Tapeti.Flow.$($version.NuGetVersion).nupkg -apikey $nugetkey -Source https://www.nuget.org/api/v2/package

  Write-Host "Pushing Tapeti.SimpleInjector.csproj" -Foreground Blue
  NuGet.exe push publish\X2Software.Tapeti.SimpleInjector.$($version.NuGetVersion).nupkg -apikey $nugetkey -Source https://www.nuget.org/api/v2/package
}
else
{
  Write-Host "Skipping push" -Foreground Blue
}