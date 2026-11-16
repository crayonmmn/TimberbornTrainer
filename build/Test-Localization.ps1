[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Compiler.ps1')
Import-ExistingCompiler $PSHOME
$checks=0
function Check([bool]$ok,[string]$why){$script:checks++;if(!$ok){throw $why}}
$pack=Join-Path $root 'dist/unified/TimberbornTrainer'
$utf8=[Text.UTF8Encoding]::new($false,$true)
$en=Get-Content (Join-Path $root 'Languages/en-US.json') -Raw|ConvertFrom-Json -AsHashtable
$zh=Get-Content (Join-Path $root 'Languages/zh-CN.json') -Raw|ConvertFrom-Json -AsHashtable
Check ($en.Count-eq478-and$zh.Count-eq478) 'Expected 477 existing entries plus global-switch help'
Check (!(Compare-Object @($en.Keys|Sort-Object) @($zh.Keys|Sort-Object))) 'Language keys differ'
Check (@($en.Values|Where-Object{[string]::IsNullOrWhiteSpace($_)}).Count-eq0) 'Empty English entry'
Check (@($zh.Values|Where-Object{[string]::IsNullOrWhiteSpace($_)}).Count-eq0) 'Empty Chinese entry'
Check (@($en.GetEnumerator()|Where-Object{$_.Key-ne't0222'-and$_.Value-match'[\u3400-\u9fff]'}).Count-eq0) 'Untranslated English catalog entry'
Check ($en.t0222-ceq'Language / 语言'-and$zh.t0222-ceq'Language / 语言') 'Intentional bilingual language label'
Check ($en.t0321-ceq'Enable global changes'-and$zh.t0321-ceq'启用全局修改'-and$en.t0478.Length-gt0-and$zh.t0478.Length-gt0) 'Global-switch labels and help'
foreach($lang in @('en-US','zh-CN')){
 $source=Join-Path $root ('Languages/'+$lang+'.json');$installed=Join-Path $pack ('Languages/'+$lang+'.json')
 Check ($utf8.GetString([IO.File]::ReadAllBytes($source)).Length-gt0) 'Invalid UTF8 source'
 Check ((Get-FileHash $source).Hash-eq(Get-FileHash $installed).Hash) 'Package language resources differ'
}
$missing=@();$unexpected=@();$keys=@()
foreach($file in Get-ChildItem (Join-Path $root 'Core'),(Join-Path $root 'Modules'),(Join-Path $root 'UI'),(Join-Path $root 'Patches') -Recurse -Filter '*.cs'){
 $text=Get-Content $file.FullName -Raw
 foreach($match in [regex]::Matches($text,'TextToken.Key\("(t\d{4})"\)')){ $id=$match.Groups[1].Value;$keys+=$id;if(!$en.ContainsKey($id)){$missing+=$file.Name+':'+$id} }
 foreach($token in [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text).GetRoot().DescendantTokens()){
  if($token.RawKind-eq[int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::StringLiteralToken-and$token.ValueText-match'[\u3400-\u9fff]'-and$token.ValueText-ne'简体中文'){$unexpected+=$file.Name+':'+$token.ValueText}
 }
}
Check ($missing.Count-eq0) ('Unknown source key: '+($missing-join','))
Check ($unexpected.Count-eq0) ('Owned text outside external resources: '+($unexpected-join','))
Check ($keys.Count-ge534) 'Missing localized source call sites'
$m=Get-Content (Join-Path $pack 'manifest.json') -Raw|ConvertFrom-Json
Check ($m.Id-ceq'TimberbornTrainer.Core') 'Mod ID changed'
Check ($m.Version-ceq'0.6.0'-and$m.MinimumGameVersion-ceq'1.1.2.8') 'Version mismatch'
Check (@($m.RequiredMods).Count-eq0) 'Unexpected Mod dependency'
$dll=Join-Path $pack 'TimberbornTrainer.dll'
Check ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()-eq'0.6.0.0') 'Assembly version mismatch'
$files=@(Get-ChildItem $pack -File -Recurse)
Check (@($files|Where-Object Extension -eq '.dll').Count-eq1) 'Unexpected dependency DLL'
Check ($files.Count-eq11) 'Unexpected unified package file count'
Check (@($files|Where-Object{$_.Extension-in'.pdb','.cs','.timber','.log','.ps1'}).Count-eq0) 'Developer files in package'
Check ((Test-Path (Join-Path $pack LICENSE))-and(Test-Path (Join-Path $pack THIRD_PARTY_NOTICES.md))) 'License missing'
$s=[IO.File]::OpenRead($dll);$p=[Reflection.PortableExecutable.PEReader]::new($s)
try{
 $r=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($p)
 $refs=@(foreach($h in $r.AssemblyReferences){$r.GetString($r.GetAssemblyReference($h).Name)})
 Check (@($refs|Where-Object{$_-match'Harmony|BepInEx|Roslyn|CodeAnalysis'}).Count-eq0) 'New runtime framework dependency'
 Check ($r.ManifestResources.Count-eq0) 'Language data embedded in DLL'
 $languageSource=Get-Content (Join-Path $root 'Core/RuntimeText.cs') -Raw
 $sessionSource=Get-Content (Join-Path $root 'Core/TrainerMod.cs') -Raw
 Check (($languageSource+$sessionSource)-notmatch'CurrentLanguage|SelectInitial|ILocalizationService|gameLanguage|Normalize\(') 'Automatic game-language dependency remains'
 $constructors=0; $constructorParameters=0
 foreach($h in $r.TypeDefinitions){$t=$r.GetTypeDefinition($h);if($r.GetString($t.Name)-eq'TrainerSession'){foreach($mh in $t.GetMethods()){$method=$r.GetMethodDefinition($mh);if($r.GetString($method.Name)-eq'.ctor'){$constructors++;foreach($ph in $method.GetParameters()){if($r.GetParameter($ph).SequenceNumber-gt0){$constructorParameters++}}}}}}
 Check ($constructors-eq1) 'Ambiguous Bindito constructor'
 Check ($constructorParameters-eq0) 'Session still requests a game-language service'
}finally{$p.Dispose();$s.Dispose()}
Write-Output ("Unified localization/package static checks: $checks passed; $($en.Count) external keys. Not in-game validation.")
