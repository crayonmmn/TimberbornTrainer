[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$GamePath, [string]$CompilerDirectory = $PSHOME)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Compiler.ps1')
Import-ExistingCompiler $CompilerDirectory
$jsonDll = Join-Path $GamePath 'Timberborn_Data/Managed/Newtonsoft.Json.dll'
$references = @(([string][AppContext]::GetData('TRUSTED_PLATFORM_ASSEMBLIES')).Split([IO.Path]::PathSeparator))
$references = @($references | Where-Object { [IO.Path]::GetFileName($_) -ne 'Newtonsoft.Json.dll' }) + @($jsonDll)
$testRoot = Join-Path $projectRoot 'TestResults'
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$testDll = Join-Path $testRoot 'TimberbornTrainer.ConfigTests.dll'
$sources = @('Core/Config.cs','Core/TrainerState.cs','Modules/ScienceCommands.cs','Modules/ConstructionRules.cs','Tests/ConfigTests.cs','Tests/ScienceTests.cs','Tests/ConstructionTests.cs') | ForEach-Object {Join-Path $projectRoot $_}
Invoke-SourceCompilation -Sources $sources -References $references -Output $testDll -Name 'TimberbornTrainer.ConfigTests'
[Reflection.Assembly]::LoadFrom($jsonDll) | Out-Null
[Reflection.Assembly]::LoadFrom($testDll) | Out-Null
$isolated = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
[TimberbornTrainer.Tests.ConfigTests]::Run($isolated)
[TimberbornTrainer.Tests.ScienceTests]::Run()
[TimberbornTrainer.Tests.ConstructionTests]::Run()
