$ErrorActionPreference = 'Stop'

function step($command) {
    Write-Host ([Environment]::NewLine + $command.ToString().Trim()) -ForegroundColor CYAN
    & $command
    if ($lastexitcode -ne 0) { throw $lastexitcode }
}

$Env:DOTNET_NOLOGO = 'true'
$Env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

step { dotnet msbuild /t:Restore /p:Configuration=Release }
pushd dotnet-xdt.tests
step { dotnet fixie --configuration Release --report test-results.xml }
