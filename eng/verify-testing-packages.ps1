[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
$corePackage = Get-ChildItem -LiteralPath $packageDirectory -Filter 'Microsoft.Azure.Functions.Worker.Testing.*.nupkg' |
    Where-Object Name -NotLike '*.symbols.nupkg' |
    Select-Object -First 1
$aspPackage = Get-ChildItem -LiteralPath $packageDirectory -Filter 'Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.Testing.*.nupkg' |
    Where-Object Name -NotLike '*.symbols.nupkg' |
    Select-Object -First 1

if (-not $corePackage -or -not $aspPackage) {
    throw "Both testing preview packages must exist in '$packageDirectory'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-Nuspec {
    param([Parameter(Mandatory)] [IO.FileInfo] $Package)

    $archive = [IO.Compression.ZipFile]::OpenRead($Package.FullName)
    try {
        $entry = $archive.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
        if (-not $entry) {
            throw "Package '$($Package.Name)' has no nuspec."
        }

        $reader = [IO.StreamReader]::new($entry.Open())
        try {
            return [xml]$reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ExactDependencies {
    param([Parameter(Mandatory)] [xml] $Nuspec)

    $dependencies = $Nuspec.package.metadata.dependencies.group.dependency
    foreach ($dependency in $dependencies) {
        if ($dependency.version -notmatch '^\[[^,\]]+\]$') {
            throw "Dependency '$($dependency.id)' must use a closed exact range; found '$($dependency.version)'."
        }
    }
}

$coreNuspec = Read-Nuspec $corePackage
$aspNuspec = Read-Nuspec $aspPackage
Assert-ExactDependencies $coreNuspec
Assert-ExactDependencies $aspNuspec

$requiredFrameworks = @('net8.0', 'net9.0', 'net10.0')
foreach ($nuspec in @($coreNuspec, $aspNuspec)) {
    $frameworks = @($nuspec.package.metadata.dependencies.group | ForEach-Object targetFramework)
    foreach ($required in $requiredFrameworks) {
        if ($required -notin $frameworks) {
            throw "Package '$($nuspec.package.metadata.id)' is missing dependency group '$required'."
        }
    }
}

$smokeProject = Join-Path $root 'test\PackageValidation\TestingPackages\TestingPackages.csproj'
$testingVersion = $coreNuspec.package.metadata.version
$aspTestingVersion = $aspNuspec.package.metadata.version
$workerDependency = $coreNuspec.package.metadata.dependencies.group[0].dependency |
    Where-Object id -EQ 'Microsoft.Azure.Functions.Worker' |
    Select-Object -First 1
$workerVersion = $workerDependency.version.Trim('[', ']')
$configFile = Join-Path $root 'test\PackageValidation\TestingPackages\NuGet.Config'
$previousFeed = $env:TESTING_PACKAGE_FEED
$env:TESTING_PACKAGE_FEED = $packageDirectory

try {
    & dotnet restore $smokeProject `
        --configfile $configFile `
        -p:TestingPackageVersion=$testingVersion `
        -p:AspNetCoreTestingPackageVersion=$aspTestingVersion `
        -p:WorkerPackageVersion=$workerVersion
    if ($LASTEXITCODE -ne 0) {
        throw 'Fresh package compile-smoke restore failed.'
    }

    & dotnet build $smokeProject -c Release --no-restore `
        -p:TestingPackageVersion=$testingVersion `
        -p:AspNetCoreTestingPackageVersion=$aspTestingVersion `
        -p:WorkerPackageVersion=$workerVersion
    if ($LASTEXITCODE -ne 0) {
        throw 'Fresh package compile-smoke build failed.'
    }

    $skewOutput = & dotnet restore $smokeProject `
        --configfile $configFile `
        -p:TestingPackageVersion=$testingVersion `
        -p:AspNetCoreTestingPackageVersion=$aspTestingVersion `
        -p:WorkerPackageVersion=2.51.0 `
        --force 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw 'Intentional Worker package skew unexpectedly restored.'
    }
    if (($skewOutput -join "`n") -notmatch 'NU1107|NU1605|version conflict|package downgrade') {
        throw "Intentional skew failed for an unexpected reason:`n$($skewOutput -join "`n")"
    }
}
finally {
    $env:TESTING_PACKAGE_FEED = $previousFeed
}

Write-Host 'Testing package exact-range, target, fresh-install, and skew verification passed.'
