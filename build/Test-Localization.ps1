param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$projectRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Compiler.ps1')
Import-ExistingCompiler $PSHOME
Initialize-TextLocalization 'en' $projectRoot
$script:checks=0
function Check([bool]$Ok,[string]$Message){++$script:checks;if(!$Ok){throw $Message}}
$parse=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp8)
$unmapped=[Collections.Generic.List[string]]::new()
$changedNonText=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem (Join-Path $projectRoot Core),(Join-Path $projectRoot Modules),(Join-Path $projectRoot UI),(Join-Path $projectRoot Patches) -Recurse -Filter '*.cs') {
  $text=[IO.File]::ReadAllText($file.FullName)
  $before=@([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text,$parse).GetRoot().DescendantTokens())
  foreach($token in $before){if($token.RawKind -eq [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::StringLiteralToken -and $token.ValueText -match '[\u3400-\u9fff]' -and !$script:LocalizedStrings.ContainsKey($token.ValueText)){$unmapped.Add($file.Name+': '+$token.ValueText)}}
  $converted=Convert-LocalizedSource $text $file.FullName $parse
  $after=@([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($converted,$parse).GetRoot().DescendantTokens())
  if($before.Count -ne $after.Count){$changedNonText.Add($file.Name);continue}
  for($i=0;$i -lt $before.Count;$i++){if($before[$i].RawKind -ne $after[$i].RawKind -or ($before[$i].RawKind -ne [int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::StringLiteralToken -and $before[$i].Text -cne $after[$i].Text)){$changedNonText.Add($file.Name);break}}
}
Check ($unmapped.Count -eq 0) ('Unmapped owned text: '+($unmapped -join '; '))
Check ($changedNonText.Count -eq 0) 'Localization changed non-string syntax tokens'
Check (@($script:LocalizedStrings.Values | Where-Object {$_ -match '[\u3400-\u9fff]'}).Count -eq 0) 'English catalog contains untranslated Han text'
Check ($script:LocalizedStrings['资源管理'] -ceq 'Resource Management') 'Resource terminology'
Check ($script:LocalizedStrings['建造与生产'] -ceq 'Building & Production') 'Construction terminology'
Check ($script:LocalizedStrings['海狸管理'] -ceq 'Beaver Management') 'Beaver terminology'
$sample='class TextProbe { string x = "当前数量：" + amount + nativeName; string id = "ModGood_测试"; }'
$translated=Convert-LocalizedSource $sample (Join-Path $projectRoot 'UI/TextProbe.cs') $parse
Check ($translated.Contains('"Current amount: " + amount + nativeName')) 'Dynamic numbers and native names changed'
Check ($translated.Contains('"ModGood_测试"')) 'Unknown/native good ID was translated'
Initialize-TextLocalization 'zh-CN' $projectRoot
Check ((Convert-LocalizedSource $sample 'UI/TextProbe.cs' $parse) -ceq $sample) 'Chinese source must remain unchanged'
$opcodes=@{}
[Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static') | ForEach-Object {$op=$_.GetValue($null);$opcodes[[int]$op.Value -band 65535]=$op}
function Read-Bodies([string]$Path) {
  $stream=[IO.File]::OpenRead($Path);$pe=[Reflection.PortableExecutable.PEReader]::new($stream)
  try {
    $reader=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    $bodies=@{};$strings=[Collections.Generic.List[string]]::new()
    foreach($handle in $reader.MethodDefinitions){
      $method=$reader.GetMethodDefinition($handle)
      if($method.RelativeVirtualAddress -eq 0){continue}
      $il=[Reflection.Metadata.PEReaderExtensions]::GetMethodBody($pe,$method.RelativeVirtualAddress).GetILBytes()
      $normalized=[byte[]]$il.Clone();$offset=0
      while($offset -lt $il.Length){
        $code=[int]$il[$offset++];if($code -eq 254){$code=65024+[int]$il[$offset++]}
        $kind=$opcodes[$code].OperandType.ToString()
        $size=switch($kind){InlineNone{0} ShortInlineI{1} ShortInlineVar{1} ShortInlineBrTarget{1} InlineVar{2} InlineI8{8} InlineR{8} InlineSwitch{4+4*[BitConverter]::ToInt32($il,$offset)} default{4}}
        if($kind -eq 'InlineString'){
          $token=[BitConverter]::ToInt32($il,$offset) -band 0xffffff
          $strings.Add($reader.GetUserString([Reflection.Metadata.Ecma335.MetadataTokens]::UserStringHandle($token)))
          [Array]::Clear($normalized,$offset,4)
        }
        $offset+=$size
      }
      $bodies[[Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($handle)]=[Convert]::ToBase64String($normalized)
    }
    return [pscustomobject]@{Bodies=$bodies;Strings=$strings}
  } finally {$pe.Dispose();$stream.Dispose()}
}
$enRoot=Join-Path $projectRoot 'dist/en/TimberbornTrainer';$zhRoot=Join-Path $projectRoot 'dist/zh-CN/TimberbornTrainer'
$enDll=Join-Path $enRoot 'TimberbornTrainer.dll';$zhDll=Join-Path $zhRoot 'TimberbornTrainer.dll'
$en=Read-Bodies $enDll;$zh=Read-Bodies $zhDll
Check ($en.Bodies.Count -eq $zh.Bodies.Count) 'Different method counts'
$changed=@($zh.Bodies.Keys | Where-Object {$zh.Bodies[$_] -cne $en.Bodies[$_]})
Check ($changed.Count -eq 0) ('Non-text method IL differs: '+($changed -join ','))
Check (@($en.Strings | Where-Object {$_ -match '[\u3400-\u9fff]'}).Count -eq 0) 'English DLL contains owned Chinese message literals'
Check ($zh.Strings.Contains('海狸修改工具')) 'Chinese title missing'
Check ($en.Strings.Contains('Timberborn Trainer')) 'English title missing'
$enManifest=Get-Content (Join-Path $enRoot manifest.json) -Raw | ConvertFrom-Json
$zhManifest=Get-Content (Join-Path $zhRoot manifest.json) -Raw | ConvertFrom-Json
Check ($enManifest.Id -ceq 'TimberbornTrainer.Core' -and $zhManifest.Id -ceq $enManifest.Id) 'Mod IDs differ'
Check ($enManifest.Version -ceq $zhManifest.Version -and $enManifest.MinimumGameVersion -ceq $zhManifest.MinimumGameVersion) 'Version declarations differ'
Check (@($enManifest.RequiredMods).Count -eq 0 -and @($zhManifest.RequiredMods).Count -eq 0) 'Unexpected dependency'
Check ([Reflection.AssemblyName]::GetAssemblyName($enDll).Version -eq [Reflection.AssemblyName]::GetAssemblyName($zhDll).Version) 'Assembly versions differ'
foreach($package in @($enRoot,$zhRoot)) {
  $paths=@(Get-ChildItem $package -File -Recurse | ForEach-Object {$_.FullName.Substring($package.Length+1)})
  Check ($paths.Count -eq 10 -and @($paths | Where-Object {$_ -match '\.(pdb|cs|timber|log)$'}).Count -eq 0) 'Unexpected package files'
}
Write-Output ("Localization/package checks: $script:checks passed. Method bodies compared: " + $en.Bodies.Count + '. No game/UI execution.')
