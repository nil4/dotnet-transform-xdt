$ErrorActionPreference = 'Stop'

function step($command) {
    Write-Host ([Environment]::NewLine + $command.ToString().Trim()) -ForegroundColor CYAN
    & $command
    if ($lastexitcode -ne 0) { throw $lastexitcode }
}

$Env:DOTNET_NOLOGO = 'true'
$Env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

step { dotnet msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=lib  /p:ContinuousIntegrationBuild=true }
step { dotnet msbuild dotnet-xdt /t:Pack    /p:Configuration=Release /p:As=lib  /p:ContinuousIntegrationBuild=true }

step { dotnet msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=tool /p:ContinuousIntegrationBuild=true }
step { dotnet msbuild dotnet-xdt /t:Pack    /p:Configuration=Release /p:As=tool /p:ContinuousIntegrationBuild=true }

step { dotnet msbuild dotnet-xdt /t:Restore /p:Configuration=Release /p:As=exe  /p:ContinuousIntegrationBuild=true }
step { dotnet msbuild dotnet-xdt /t:Build   /p:Configuration=Release /p:As=exe  /p:ContinuousIntegrationBuild=true }
