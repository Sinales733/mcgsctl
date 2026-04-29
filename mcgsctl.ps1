$ErrorActionPreference = 'Stop'

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $ToolRoot 'McgsCtl.csproj'

dotnet run --project $Project -- @args
exit $LASTEXITCODE
