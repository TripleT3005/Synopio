# Parameters
param(
    [string]$Directory = $PSScriptRoot,
    [string]$Pandoc = 'pandoc',
    [string]$Browser = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
)

$ErrorActionPreference = 'Stop'
$markdownFormat = 'markdown-fancy_lists-example_lists-startnum'
$encoding = New-Object Text.UTF8Encoding $false

# Hyphenation
function Add-SoftHyphens([string]$Path, [string[]]$Words) {
    $content = [IO.File]::ReadAllText($Path, $encoding)
    $bodyStart = $content.IndexOf('<body')
    $head = $content.Substring(0, $bodyStart)
    $body = $content.Substring($bodyStart)
    foreach ($word in $Words) {
        $pattern = '(?<=[>\s(])' + [Text.RegularExpressions.Regex]::Escape($word.Replace('-', '')) + '(?=[\s.,;:)<])'
        $body = [Text.RegularExpressions.Regex]::Replace($body, $pattern, $word.Replace('-', '&shy;'))
    }
    [IO.File]::WriteAllText($Path, $head + $body, $encoding)
}

# Conversion
$hyphenation = [IO.File]::ReadAllLines((Join-Path $Directory 'hyphenation.txt'), $encoding) | Where-Object { $_.Trim().Length -gt 0 }
$names = Get-ChildItem -Path $Directory -Filter '*.md' | Where-Object { $_.Name -ne 'README.md' } | ForEach-Object { $_.BaseName }
$workDirectory = Join-Path ([IO.Path]::GetTempPath()) ('synopio-testdata-' + [Guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $workDirectory | Out-Null

try {
    foreach ($name in $names) {
        $markdown = Join-Path $Directory "$name.md"
        $stylesheet = Join-Path $Directory "layout-$name.css"
        $html = Join-Path $workDirectory "$name.html"
        $pdf = Join-Path $Directory "$name.pdf"

        & $Pandoc $markdown --from $markdownFormat --to plain --wrap auto --columns 72 --output (Join-Path $Directory "$name.txt")
        & $Pandoc $markdown --from $markdownFormat --to docx --output (Join-Path $Directory "$name.docx")
        & $Pandoc $markdown --from $markdownFormat --to html5 --standalone --embed-resources --css $stylesheet --metadata pagetitle=$name --metadata lang=de --output $html
        Add-SoftHyphens $html $hyphenation

        $arguments = @('--headless', '--disable-gpu', '--disable-sync', '--no-first-run', '--no-pdf-header-footer', "--user-data-dir=$workDirectory\profile", "--print-to-pdf=$pdf", ([Uri]$html).AbsoluteUri)
        Start-Process -FilePath $Browser -ArgumentList $arguments -Wait -NoNewWindow -RedirectStandardError (Join-Path $workDirectory 'browser.log')
    }
}
finally {
    Remove-Item -Recurse -Force -Path $workDirectory -ErrorAction SilentlyContinue
}
