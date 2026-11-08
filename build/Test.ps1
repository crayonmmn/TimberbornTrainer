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
$sources = @('Core/Config.cs','Core/TrainerState.cs','Modules/ScienceCommands.cs','Modules/ConstructionRules.cs','Modules/InventoryCommands.cs','Modules/NeedLock.cs','Modules/InventoryDescription.cs','Modules/InventoryBrowser.cs','Tests/InventoryBrowserTests.cs','Tests/ConfigTests.cs','Tests/ScienceTests.cs','Tests/ConstructionTests.cs','Tests/SurvivalTests.cs') | ForEach-Object {Join-Path $projectRoot $_}
Invoke-SourceCompilation -Sources $sources -References $references -Output $testDll -Name 'TimberbornTrainer.ConfigTests'
[Reflection.Assembly]::LoadFrom($jsonDll) | Out-Null
[Reflection.Assembly]::LoadFrom($testDll) | Out-Null
$isolated = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
[TimberbornTrainer.Tests.ConfigTests]::Run($isolated)
[TimberbornTrainer.Tests.ScienceTests]::Run()
[TimberbornTrainer.Tests.ConstructionTests]::Run()
[TimberbornTrainer.Tests.SurvivalTests]::Run()
[TimberbornTrainer.Tests.InventoryBrowserTests]::Run()
$lifecycleDll = Join-Path $testRoot 'TimberbornTrainer.BeaverLifecycleTests.dll'
$lifecycleSources = @('Modules/BeaverModule.cs','Tests/BeaverLifecycleTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $lifecycleSources -References @($references + @($testDll)) -Output $lifecycleDll -Name 'TimberbornTrainer.BeaverLifecycleTests'
[Reflection.Assembly]::LoadFrom($lifecycleDll) | Out-Null
[TimberbornTrainer.Tests.BeaverLifecycleTests]::Run()
$inventoryDll = Join-Path $testRoot 'TimberbornTrainer.InventoryCatalogTests.dll'
$inventorySources = @('Modules/NativeInventoryCatalog.cs','Tests/InventoryCatalogTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $inventorySources -References @($references + @($testDll)) -Output $inventoryDll -Name 'TimberbornTrainer.InventoryCatalogTests'
[Reflection.Assembly]::LoadFrom($inventoryDll) | Out-Null
[TimberbornTrainer.Tests.InventoryCatalogTests]::Run()
$planDll = Join-Path $testRoot 'TimberbornTrainer.InventoryWritePlanTests.dll'
$planSources = @('Modules/InventoryWritePlan.cs','Tests/InventoryWritePlanTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
# Standalone pure layer: no game DLL, Newtonsoft, Unity or fake game types in references.
$planReferences = @(([string][AppContext]::GetData('TRUSTED_PLATFORM_ASSEMBLIES')).Split([IO.Path]::PathSeparator))
$planReferences = @($planReferences | Where-Object { [IO.Path]::GetFileName($_) -ne 'Newtonsoft.Json.dll' })
Invoke-SourceCompilation -Sources $planSources -References $planReferences -Output $planDll -Name 'TimberbornTrainer.InventoryWritePlanTests'
[Reflection.Assembly]::LoadFrom($planDll) | Out-Null
[TimberbornTrainer.Tests.InventoryWritePlanTests]::Run()
