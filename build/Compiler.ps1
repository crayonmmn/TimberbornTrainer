# Uses an existing Roslyn compiler. Does not install, download, restore or execute repository helpers.
function Import-ExistingCompiler([string]$Directory) {
  foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    $path = Join-Path $Directory $name
    if (!(Test-Path -LiteralPath $path)) { throw '未找到已安装的 Roslyn。请指定 CompilerDirectory，或使用 README 中的 SDK 构建流程。' }
    [Reflection.Assembly]::LoadFrom($path) | Out-Null
  }
}

function Invoke-SourceCompilation {
  param([string[]]$Sources, [string[]]$References, [string]$Output, [string]$Name)
  $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp8)
  $trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
  foreach ($source in ($Sources | Sort-Object)) {
    $text = [IO.File]::ReadAllText($source)
    $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text, $parseOptions, $source, [Text.Encoding]::UTF8))
  }
  $metadata = [System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
  foreach ($reference in ($References | Sort-Object -Unique)) {
    $metadata.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($reference))
  }
  $options = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new(
    [Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel(
    [Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithDeterministic($true)
  $compilation = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create($Name, $trees, $metadata, $options)
  $temporary = $Output + '.tmp'
  $stream = [IO.File]::Create($temporary)
  try { $result = $compilation.Emit($stream) }
  finally { $stream.Dispose() }
  foreach ($diagnostic in $result.Diagnostics) { Write-Output $diagnostic.ToString() }
  if (!$result.Success) {
    Remove-Item -LiteralPath $temporary
    throw '编译失败。'
  }
  Move-Item -LiteralPath $temporary -Destination $Output -Force
}
