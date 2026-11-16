[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$GamePath,
  [string]$CompilerDirectory = $PSHOME
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$managed = Join-Path $gameRoot 'Timberborn_Data/Managed'
$versionFile = Join-Path $gameRoot 'Timberborn_Data/StreamingAssets/Version.txt'
if ((Get-Content -LiteralPath $versionFile -Raw).Trim() -cne '1.1.2.8-ffc2de0-sw') {
  throw '游戏版本不匹配：仅支持 1.1.2.8-ffc2de0-sw。'
}
$unityVersion = (Get-Item -LiteralPath (Join-Path $gameRoot 'UnityPlayer.dll')).VersionInfo.ProductVersion
if (!$unityVersion.StartsWith('6000.5.11f1 ')) { throw 'Unity 版本不匹配。' }
foreach ($required in @('Timberborn.ModManagerScene.dll','Bindito.Core.dll','UnityEngine.UIElementsModule.dll','Newtonsoft.Json.dll','netstandard.dll')) {
  if (!(Test-Path -LiteralPath (Join-Path $managed $required))) { throw ('缺少游戏引用：' + $required) }
}
. (Join-Path $PSScriptRoot 'Compiler.ps1')
Import-ExistingCompiler $CompilerDirectory

$sources = @('Core','Modules','UI','Patches') | ForEach-Object {
  Get-ChildItem -LiteralPath (Join-Path $projectRoot $_) -Filter '*.cs' -Recurse
}
$references = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' | ForEach-Object FullName)
$outputDirectory = Join-Path $projectRoot 'bin/Release/netstandard2.1'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$output = Join-Path $outputDirectory 'TimberbornTrainer.dll'
Invoke-SourceCompilation -Sources @($sources.FullName) -References $references -Output $output -Name 'TimberbornTrainer'
$package = Join-Path $projectRoot 'dist/unified/TimberbornTrainer'
[IO.Directory]::CreateDirectory($package) | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $package 'TimberbornTrainer.dll')
foreach ($name in @('manifest.json','README.md','README.zh-CN.md','LICENSE','THIRD_PARTY_NOTICES.md')) {
  Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $package $name)
}
$packageDocs = Join-Path $package 'docs'
[IO.Directory]::CreateDirectory($packageDocs) | Out-Null
foreach ($name in @('Usage.en.md','使用说明.md','第三方代码来源记录.md')) {
  Copy-Item -LiteralPath (Join-Path $projectRoot ('docs/' + $name)) -Destination (Join-Path $packageDocs $name)
}
$languageOutput = Join-Path $package 'Languages'
[IO.Directory]::CreateDirectory($languageOutput) | Out-Null
foreach ($file in @('en-US.json','zh-CN.json')) {
  Copy-Item -LiteralPath (Join-Path $projectRoot ('Languages/' + $file)) -Destination (Join-Path $languageOutput $file) -Force
}
$manifest = Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
$manifest.Name = 'Timberborn Trainer'
$manifest.Description = 'Resource Management, Building & Production, and Beaver Management. Runtime English / Simplified Chinese.'
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding utf8NoBOM
Write-Output 'Unified Release built. Language resources are external; no installation performed.'