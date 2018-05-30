@echo off

dotnet.exe msbuild /t:Restore /p:Configuration=Release;Pack=lib
dotnet.exe msbuild /t:Pack    /p:Configuration=Release;Pack=lib

dotnet.exe msbuild /t:Restore /p:Configuration=Release;Pack=tool
dotnet.exe msbuild /t:Pack    /p:Configuration=Release;Pack=tool
