{ pkgs ? import <nixpkgs> {} }:

# Source: https://nixos.wiki/wiki/DotNET#Example:_Running_Rider_with_dotnet_.26_PowerShell

(pkgs.buildFHSEnv {
  name = "rider-env";
  targetPkgs = pkgs: (with pkgs; [
    jetbrains.rider
    dotnetCorePackages.dotnet_8.sdk
    dotnetCorePackages.dotnet_8.aspnetcore
    powershell
  ]);
  multiPkgs = pkgs: (with pkgs; [
  ]);
  runScript = "nohup rider > /dev/null 2>&1 &";
}).env
