@Echo Off
mkdir publish

REM Executable is generated using self-contained=true, which is just a wrapper for "dotnet Tapeti.Cmd.dll".
REM We don't need all the other DLL's so we'll build it twice and borrow the wrapper executable for a proper
REM framework-dependant build.
dotnet publish -c Release -r win-x64 --self-contained=true -o .\publish\selfcontained
dotnet publish -c Release -r win-x64 --self-contained=false -o .\publish

copy .\publish\selfcontained\Tapeti.Cmd.exe .\publish\