@echo off

dotnet.exe msbuild /t:Restore /p:Configuration=Release

pushd
cd dotnet-xdt.tests
dotnet.exe fixie --configuration Release --report test-results.xml
popd
