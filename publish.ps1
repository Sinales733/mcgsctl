$ErrorActionPreference = 'Stop'

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $ToolRoot 'McgsCtl.csproj'
$DistRoot = Join-Path $ToolRoot 'dist'
$Rids = @('win-x86', 'win-x64')

if (Test-Path $DistRoot) {
    Remove-Item -LiteralPath $DistRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $DistRoot | Out-Null

$Commit = 'unknown'
try {
    $Commit = (git -C $ToolRoot rev-parse --short HEAD 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($Commit)) { $Commit = 'unknown' }
} catch {
    $Commit = 'unknown'
}

$env:MCGSCTL_COMMIT = $Commit

foreach ($Rid in $Rids) {
    $OutDir = Join-Path $DistRoot $Rid
    dotnet publish $Project -c Release -r $Rid --self-contained false -o $OutDir

    foreach ($Name in @('README.md', 'DEPENDENCIES.md', 'THIRD_PARTY_NOTICES.md')) {
        $Source = Join-Path $ToolRoot $Name
        if (Test-Path $Source) { Copy-Item -LiteralPath $Source -Destination (Join-Path $OutDir $Name) -Force }
    }
    foreach ($DirName in @('docs', 'profiles', 'schemas', 'java', 'lib')) {
        $Source = Join-Path $ToolRoot $DirName
        $Dest = Join-Path $OutDir $DirName
        if (Test-Path $Source) { Copy-Item -LiteralPath $Source -Destination $Dest -Recurse -Force }
    }

    $Exe = Join-Path $OutDir 'mcgsctl.exe'
    $Dll = Join-Path $OutDir 'mcgsctl.dll'
    $ExeSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Exe).Hash.ToLowerInvariant()
    $DllSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $Dll).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $OutDir 'mcgsctl.exe.sha256') -Value "$ExeSha  mcgsctl.exe" -Encoding UTF8

    $HashLines = Get-ChildItem -LiteralPath $OutDir -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            $Rel = $_.FullName.Substring($OutDir.Length).TrimStart('\', '/')
            "$Hash  $Rel"
        }
    $HashLines | Set-Content -LiteralPath (Join-Path $OutDir 'SHA256SUMS.txt') -Encoding UTF8

    $Version = [ordered]@{
        name = 'mcgsctl'
        version = '0.4.0'
        commit = $Commit
        targetRuntime = $Rid
        selfContained = $false
        framework = 'net7.0-windows'
        builtAt = (Get-Date).ToString('O')
        executableSha256 = $ExeSha
        assemblySha256 = $DllSha
        profileSchema = 1
        workflowResultSchema = 1
    }
    $Version | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutDir 'version.json') -Encoding UTF8

    Write-Host "Published $Exe"
    Write-Host "SHA256 $ExeSha"
}
