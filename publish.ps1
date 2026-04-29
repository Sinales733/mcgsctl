$ErrorActionPreference = 'Stop'

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $ToolRoot 'McgsCtl.csproj'
$OutDir = Join-Path $ToolRoot 'dist\win-x64'

if (Test-Path $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}

$Commit = 'unknown'
try {
    $Commit = (git -C $ToolRoot rev-parse --short HEAD 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($Commit)) { $Commit = 'unknown' }
} catch {
    $Commit = 'unknown'
}

$env:MCGSCTL_COMMIT = $Commit
dotnet publish $Project -c Release -r win-x64 --self-contained false -o $OutDir

$Exe = Join-Path $OutDir 'mcgsctl.exe'
$Sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Exe).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $OutDir 'mcgsctl.exe.sha256') -Value "$Sha  mcgsctl.exe" -Encoding UTF8

$Dll = Join-Path $OutDir 'mcgsctl.dll'
$DllSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Dll).Hash.ToLowerInvariant()
$HashLines = Get-ChildItem -LiteralPath $OutDir -File |
    Where-Object { $_.Name -in @('mcgsctl.exe', 'mcgsctl.dll', 'mcgsctl.deps.json', 'mcgsctl.runtimeconfig.json') } |
    Sort-Object Name |
    ForEach-Object {
        $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        "$Hash  $($_.Name)"
    }
$HashLines | Set-Content -LiteralPath (Join-Path $OutDir 'SHA256SUMS.txt') -Encoding UTF8

$Version = [ordered]@{
    name = 'mcgsctl'
    commit = $Commit
    targetRuntime = 'win-x64'
    selfContained = $false
    framework = 'net7.0-windows'
    builtAt = (Get-Date).ToString('O')
    executableSha256 = $Sha
    assemblySha256 = $DllSha
}
$Version | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutDir 'version.json') -Encoding UTF8

@'
# Dependencies

- .NET 7 Windows Desktop Runtime is required for the framework-dependent build.
- Java 21 JRE/JDK is required for the Jackcess-based `.MCE` read-only exporter.
- Bundled Java libraries under `lib/` are required by the exporter.
- MCGS embedded editor must be installed locally and run on a visible desktop session.
'@ | Set-Content -LiteralPath (Join-Path $OutDir 'DEPENDENCIES.md') -Encoding UTF8

Write-Host "Published $Exe"
Write-Host "SHA256 $Sha"
