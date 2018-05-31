@echo off
setlocal

dotnet.exe msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=lib
dotnet.exe msbuild dotnet-xdt /t:Pack    /p:Configuration=Release /p:As=lib

dotnet.exe msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=tool
dotnet.exe msbuild dotnet-xdt /t:Pack    /p:Configuration=Release /p:As=tool

dotnet.exe msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=exe
dotnet.exe msbuild dotnet-xdt /t:Build   /p:Configuration=Release /p:As=exe

dotnet.exe msbuild /t:Restore /p:Configuration=Release

pushd
cd dotnet-xdt.tests
dotnet.exe fixie --configuration Release
popd


