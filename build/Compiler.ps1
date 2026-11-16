# Uses an existing Roslyn compiler. Does not install, download, restore or execute repository helpers.
function Import-ExistingCompiler([string]$Directory) {
  foreach ($name in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    $path = Join-Path $Directory $name
    if (!(Test-Path -LiteralPath $path)) { throw '未找到已安装的 Roslyn。请指定 CompilerDirectory，或使用 README 中的 SDK 构建流程。' }
    [Reflection.Assembly]::LoadFrom($path) | Out-Null
  }
}

function Initialize-TextLocalization([string]$Language, [string]$ProjectRoot) {
  $script:OutputLanguage = $Language
    $zh = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Languages/zh-CN.json') -Raw | ConvertFrom-Json -AsHashtable
  $en = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Languages/en-US.json') -Raw | ConvertFrom-Json -AsHashtable
  $script:LocalizedStrings = @{}
  foreach ($key in $zh.Keys) { $script:LocalizedStrings[$zh[$key]] = $en[$key] }
  $script:TestTextExpectations = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Tests/TextExpectations.en.json') -Raw | ConvertFrom-Json -AsHashtable
  $script:LocalizedLiteralCount = 0
}

function Convert-LocalizedSource([string]$Text, [string]$Source, $ParseOptions) {
  # Only legacy regression expectations are localized at compile time. Production
  # uses external runtime keys; bilingual tests must retain both expected languages.
  if ($Source -notmatch '[\\/]Tests[\\/]' -or $Source -match 'Runtime(?:Ui)?LocalizationTests\.cs$') { return $Text }
  if ($script:OutputLanguage -ne 'en') { return $Text }
  $parsed = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($Text, $ParseOptions)
  $tokens = @($parsed.GetRoot().DescendantTokens() | Where-Object RawKind -EQ ([int][Microsoft.CodeAnalysis.CSharp.SyntaxKind]::StringLiteralToken) | Sort-Object { $_.Span.Start } -Descending)
  $testSource = $Source -match '[\\/]Tests[\\/]'
  foreach ($token in $tokens) {
    $value = $token.ValueText
    if ($testSource -and $script:TestTextExpectations.ContainsKey($value)) { $value = $script:TestTextExpectations[$value] }
    elseif ($script:LocalizedStrings.ContainsKey($value)) { $value = $script:LocalizedStrings[$value] }
    else { continue }
    # Replace literal tokens only. No identifiers, expression structure, runtime values or source files change.
    $literal = [Microsoft.CodeAnalysis.CSharp.SyntaxFactory]::Literal([string]$value).Text
    $Text = $Text.Remove($token.Span.Start, $token.Span.Length).Insert($token.Span.Start, $literal)
    ++$script:LocalizedLiteralCount
  }
  return $Text
}

function Invoke-SourceCompilation {
  param([string[]]$Sources, [string[]]$References, [string]$Output, [string]$Name)
  $parseOptions = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp8)
  $trees = [System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
  foreach ($source in ($Sources | Sort-Object)) {
    $text = [IO.File]::ReadAllText($source)
    if (Get-Variable OutputLanguage -Scope Script -ErrorAction SilentlyContinue) {
      $text = Convert-LocalizedSource $text $source $parseOptions
      if ($source -match '[\\/]Tests[\\/]' -and $source -notmatch '(?:LocalizedAssertions|Runtime(?:Ui)?LocalizationTests)\.cs$') { $text = "using TimberbornTrainer.Localization;`n" + $text.Replace(".Contains(", ".ContainsLocalized(") }
    }
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
  foreach ($diagnostic in $result.Diagnostics) { if ($diagnostic.Severity -ne [Microsoft.CodeAnalysis.DiagnosticSeverity]::Hidden) { Write-Output $diagnostic.ToString() } }
  if (!$result.Success) {
    Remove-Item -LiteralPath $temporary
    throw '编译失败。'
  }
  Move-Item -LiteralPath $temporary -Destination $Output -Force
}
