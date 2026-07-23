[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildJob = Get-Content -Raw (Join-Path $root 'eng\ci\templates\official\jobs\build-artifacts.yml')
$release = Get-Content -Raw (Join-Path $root 'eng\ci\official-release.yml')
$handoff = Get-Content -Raw (Join-Path $root 'docs\testing-upstream-handoff.md')
$projects = @(
    (Join-Path $root 'src\DotNetWorker.Testing\DotNetWorker.Testing.csproj'),
    (Join-Path $root 'extensions\Worker.Extensions.Http.AspNetCore.Testing\src\Worker.Extensions.Http.AspNetCore.Testing.csproj')
)

if ($buildJob -match '(?im)^\s*(nuget|dotnet\s+nuget)\s+push\b|command:\s*push\b') {
    throw 'The testing artifact build job must not contain a package publish command.'
}

foreach ($packageId in @(
    'Microsoft.Azure.Functions.Worker.Testing',
    'Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing')) {
    if ($release -notmatch [Regex]::Escape("!$packageId.*.nupkg")) {
        throw "Official release patterns must explicitly exclude '$packageId'."
    }
}

$scopedText = ($projects | ForEach-Object { Get-Content -Raw $_ }) -join "`n"
if ($scopedText -match '(?i)(client[_-]?secret|access[_-]?token|api[_-]?key|password)\s*=') {
    throw 'Testing package project files must not introduce credential values.'
}

if ($handoff -notmatch 'local and CI artifacts only' -or
    $handoff -notmatch 'does not publish packages' -or
    $handoff -notmatch 'does not post that text') {
    throw 'The handoff must retain the no-publication and no-external-mutation boundary.'
}

Write-Host 'Testing handoff boundary verification passed.'
