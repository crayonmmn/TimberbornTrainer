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
$package = Join-Path $projectRoot 'dist/TimberbornTrainer'
[IO.Directory]::CreateDirectory($package) | Out-Null
Copy-Item -LiteralPath $output -Destination (Join-Path $package 'TimberbornTrainer.dll')
foreach ($name in @('manifest.json','README.md','LICENSE')) {
  Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination (Join-Path $package $name)
}
Write-Output '构建成功。仅在项目 dist 目录生成待测试包，没有安装到游戏。'

