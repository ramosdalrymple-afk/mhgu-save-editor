[CmdletBinding()]
param(
    [string]$Version = '0.2.0-preview.5',
    [Parameter(Mandatory = $true)][string]$InnoCompiler
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid release version' }
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$compiler = (Resolve-Path -LiteralPath $InnoCompiler).Path
$releaseRoot = Join-Path $repositoryRoot ('artifacts/release/v' + $Version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$sourceRoot = Join-Path $releaseRoot 'source'
$rawPublishRoot = Join-Path $releaseRoot 'publish-raw'
$publishRoot = Join-Path $releaseRoot 'publish'
$downloadsRoot = Join-Path $releaseRoot 'downloads'
New-Item -ItemType Directory -Path $sourceRoot, $publishRoot, $downloadsRoot | Out-Null

Push-Location $repositoryRoot
try {
    if (git status --porcelain --untracked-files=no) { throw 'Commit tracked changes before building a release' }
    $commit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit' }
    $archive = Join-Path $releaseRoot 'source.zip'
    git archive --format=zip --output=$archive HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Source export failed' }
    Expand-Archive -LiteralPath $archive -DestinationPath $sourceRoot
    if (Test-Path -LiteralPath (Join-Path $sourceRoot 'src/MhguSave.App/Assets/Game')) {
        throw 'Public releases must not contain locally extracted game assets'
    }
    if (Test-Path -LiteralPath (Join-Path $sourceRoot 'src/MhguSave.Format/Data/Local')) {
        throw 'Public releases must not contain locally extracted game catalogs'
    }
}
finally { Pop-Location }

Push-Location $sourceRoot
try {
    dotnet test MhguSaveEditor.sln -c Release --runtime win-x64 -p:SelfContained=false --logger trx --results-directory (Join-Path $releaseRoot 'test-results')
    if ($LASTEXITCODE -ne 0) { throw 'Release tests failed' }
    dotnet publish src/MhguSave.App/MhguSave.App.csproj -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false -warnaserror -o $rawPublishRoot
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed' }
}
finally { Pop-Location }

# Native dependency packages may publish their own large debug symbols.
Get-ChildItem -LiteralPath $rawPublishRoot | Where-Object { $_.Extension -ne '.pdb' } |
    Copy-Item -Destination $publishRoot -Recurse

Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging/EARLY-ACCESS.txt') -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging/THIRD-PARTY-NOTICES.md') -Destination $publishRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'docs/KNOWN_ISSUES.md') -Destination (Join-Path $publishRoot 'KNOWN-ISSUES.md')
$licensesRoot = Join-Path $publishRoot 'licenses'
$armorLicenses = Join-Path $licensesRoot 'armor-icons'
New-Item -ItemType Directory -Path $armorLicenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceRoot 'src/MhguSave.App/Assets/Equipment/LICENSE.txt') -Destination $armorLicenses
Copy-Item -LiteralPath (Join-Path $sourceRoot 'src/MhguSave.App/Assets/Equipment/provenance.json') -Destination $armorLicenses
Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'packaging/licenses') -File -ErrorAction SilentlyContinue |
    Copy-Item -Destination $licensesRoot

# Retain license and notice files distributed inside the runtime's NuGet packages.
$assets = Get-Content -LiteralPath (Join-Path $sourceRoot 'src/MhguSave.App/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($libraryProperty in $assets.libraries.PSObject.Properties) {
    $library = $libraryProperty.Value
    if ($library.type -ne 'package') { continue }
    $folder = ($libraryProperty.Name -replace '[^A-Za-z0-9._-]', '-')
    foreach ($relativeFile in $library.files) {
        $leaf = [IO.Path]::GetFileName($relativeFile)
        if ($leaf -notmatch '(?i)^((licen[cs]e|notice)(\.(txt|md))?|third.?party.?notices(\.(txt|md))?|OFL\.txt)$') { continue }
        foreach ($packageFolder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path (Join-Path $packageFolder $library.path) $relativeFile
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $destination = Join-Path $licensesRoot $folder
                New-Item -ItemType Directory -Path $destination -Force | Out-Null
                Copy-Item -LiteralPath $candidate -Destination (Join-Path $destination $leaf)
                break
            }
        }
    }
}

# Self-contained runtime packs are download dependencies, not normal libraries.
foreach ($dependency in $assets.project.frameworks.'net9.0'.downloadDependencies) {
    if ($dependency.name -ine 'Microsoft.NETCore.App.Runtime.win-x64') { continue }
    if ($dependency.version -notmatch '^\[([^,]+),') { throw 'Cannot determine runtime pack version' }
    $runtimeVersion = $Matches[1].Trim()
    foreach ($packageFolder in $assets.packageFolders.PSObject.Properties.Name) {
        $runtimePackage = Join-Path (Join-Path $packageFolder $dependency.name.ToLowerInvariant()) $runtimeVersion
        if (-not (Test-Path -LiteralPath $runtimePackage)) { continue }
        $runtimeNotices = Join-Path $licensesRoot ('dotnet-runtime-' + $runtimeVersion)
        New-Item -ItemType Directory -Path $runtimeNotices -Force | Out-Null
        foreach ($notice in @('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT')) {
            Copy-Item -LiteralPath (Join-Path $runtimePackage $notice) -Destination $runtimeNotices
        }
        break
    }
}

$buildInfo = [ordered]@{
    version = $Version
    commit = $commit
    runtime = 'win-x64'
    selfContained = $true
    privateGameAssetsIncluded = $false
    source = 'https://github.com/ramosdalrymple-afk/mhgu-save-editor'
}
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishRoot 'build-info.json') -Encoding utf8

$portable = Join-Path $downloadsRoot "MHGU-Save-Editor-$Version-win-x64-Portable.zip"
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $portable -CompressionLevel Optimal
$numericVersion = ($Version -split '-')[0] + '.0'
& $compiler "/DAppVersion=$Version" "/DAppNumericVersion=$numericVersion" "/DPublishDir=$publishRoot" `
    "/DReleaseOutputDir=$downloadsRoot" (Join-Path $sourceRoot 'packaging/windows/MhguSaveEditor.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }

$installer = Join-Path $downloadsRoot "MHGU-Save-Editor-$Version-win-x64-Setup.exe"
$checksums = foreach ($asset in @($installer, $portable)) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash + '  ' + [IO.Path]::GetFileName($asset)
}
$checksums | Set-Content -LiteralPath (Join-Path $downloadsRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Release downloads: $downloadsRoot"
