[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$GamePath, [string]$CompilerDirectory = $PSHOME, [switch]$CompileOnly)
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
$sources = @('Core/Config.cs','Core/TrainerState.cs','Modules/ScienceCommands.cs','Modules/ConstructionCommands.cs','Modules/InventoryCommands.cs','Modules/NeedLock.cs','Modules/InventoryDescription.cs','Modules/InventoryBrowser.cs','Tests/InventoryBrowserTests.cs','Tests/ConfigTests.cs','Tests/ScienceTests.cs','Tests/ConstructionTests.cs','Tests/SurvivalTests.cs') | ForEach-Object {Join-Path $projectRoot $_}
Invoke-SourceCompilation -Sources $sources -References $references -Output $testDll -Name 'TimberbornTrainer.ConfigTests'
if (!$CompileOnly) {
[Reflection.Assembly]::LoadFrom($jsonDll) | Out-Null
[Reflection.Assembly]::LoadFrom($testDll) | Out-Null
$isolated = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
[TimberbornTrainer.Tests.ConfigTests]::Run($isolated)
[TimberbornTrainer.Tests.ScienceTests]::Run()
[TimberbornTrainer.Tests.ConstructionTests]::Run()
[TimberbornTrainer.Tests.SurvivalTests]::Run()
[TimberbornTrainer.Tests.InventoryBrowserTests]::Run()
}
$lifecycleDll = Join-Path $testRoot 'TimberbornTrainer.BeaverLifecycleTests.dll'
$lifecycleSources = @('Modules/BeaverModule.cs','Modules/NeedMaintenance.cs','Tests/BeaverLifecycleTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $lifecycleSources -References @($references + @($testDll)) -Output $lifecycleDll -Name 'TimberbornTrainer.BeaverLifecycleTests'
if (!$CompileOnly) {
[Reflection.Assembly]::LoadFrom($lifecycleDll) | Out-Null
[TimberbornTrainer.Tests.BeaverLifecycleTests]::Run()
}
$inventoryDll = Join-Path $testRoot 'TimberbornTrainer.InventoryCatalogTests.dll'
$inventorySources = @('Modules/NativeInventoryCatalog.cs','Modules/NativeInventoryMapSelection.cs','Modules/MapInventorySource.cs','Modules/NativeInventorySafety.cs','Modules/InventorySafetyReport.cs','Modules/NativeInventoryTiming.cs','Modules/InventoryTimingTrace.cs','Modules/NativeInventoryEditing.cs','Modules/InventoryEditCommands.cs','Modules/InventoryWritePlan.cs','Tests/InventoryCatalogTests.cs','Tests/NativeInventorySafetyTests.cs','Tests/MapSelectionTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $inventorySources -References @($references + @($testDll)) -Output $inventoryDll -Name 'TimberbornTrainer.InventoryCatalogTests'
if (!$CompileOnly) {
[Reflection.Assembly]::LoadFrom($inventoryDll) | Out-Null
[TimberbornTrainer.Tests.InventoryCatalogTests]::Run()
[TimberbornTrainer.Tests.NativeInventorySafetyTests]::Run()
[TimberbornTrainer.Tests.MapSelectionTests]::Run()
}
$planDll = Join-Path $testRoot 'TimberbornTrainer.InventoryWritePlanTests.dll'
$planSources = @('Modules/InventoryWritePlan.cs','Tests/InventoryWritePlanTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
# Standalone pure layer: no game DLL, Newtonsoft, Unity or fake game types in references.
$planReferences = @(([string][AppContext]::GetData('TRUSTED_PLATFORM_ASSEMBLIES')).Split([IO.Path]::PathSeparator))
$planReferences = @($planReferences | Where-Object { [IO.Path]::GetFileName($_) -ne 'Newtonsoft.Json.dll' })
Invoke-SourceCompilation -Sources $planSources -References $planReferences -Output $planDll -Name 'TimberbornTrainer.InventoryWritePlanTests'
if (!$CompileOnly) {
[Reflection.Assembly]::LoadFrom($planDll) | Out-Null
[TimberbornTrainer.Tests.InventoryWritePlanTests]::Run()
}
$editDll = Join-Path $testRoot 'TimberbornTrainer.InventoryEditTests.dll'
$editSources = @('Modules/InventoryEditCommands.cs','Modules/InventoryWritePlan.cs','Tests/InventoryEditTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $editSources -References @($references + @($testDll)) -Output $editDll -Name 'TimberbornTrainer.InventoryEditTests'
if (!$CompileOnly) {
  [Reflection.Assembly]::LoadFrom($editDll) | Out-Null
  [TimberbornTrainer.Tests.InventoryEditTests]::Run()
}
$weatherDll = Join-Path $testRoot 'TimberbornTrainer.WeatherTests.dll'
$weatherSources = @('Modules/WeatherModule.cs','Modules/WeatherDurationCommands.cs','Tests/WeatherTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $weatherSources -References @($references + @($testDll)) -Output $weatherDll -Name 'TimberbornTrainer.WeatherTests'
if (!$CompileOnly) {
  [Reflection.Assembly]::LoadFrom($weatherDll) | Out-Null
  [TimberbornTrainer.Tests.WeatherTests]::Run()
}
$uiDll = Join-Path $testRoot 'TimberbornTrainer.ResourceUiTests.dll'
$uiSources = @('UI/MainPanel.cs','UI/InventoryPanel.cs','UI/MapInventoryPanel.cs','UI/ControlStyles.cs','UI/Localization/Chinese.cs','Modules/MapInventorySource.cs','Modules/InventoryEditCommands.cs','Modules/InventoryWritePlan.cs','Modules/InventorySafetyReport.cs','Modules/InventoryTimingTrace.cs','Modules/WeatherDurationCommands.cs','Tests/UiTestDoubles.cs','Tests/ResourceUiTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Invoke-SourceCompilation -Sources $uiSources -References @($references + @($testDll)) -Output $uiDll -Name 'TimberbornTrainer.ResourceUiTests'
if (!$CompileOnly) {
  [Reflection.Assembly]::LoadFrom($uiDll) | Out-Null
  [TimberbornTrainer.Tests.ResourceUiTests]::Run((Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))))
} else { Write-Output '所有测试程序集编译完成；未加载或执行任何测试，不能记作测试通过。' }
