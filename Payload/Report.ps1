# Report.ps1 - shared .docx report generator
# Dot-sourced by RunAction.ps1. All reports are saved to one folder on the desktop.

function Get-ReportDir {
    $d = Join-Path ([Environment]::GetFolderPath('Desktop')) 'AS Shield reports'
    if (-not (Test-Path -LiteralPath $d)) { try { New-Item -ItemType Directory -Path $d -Force | Out-Null } catch {} }
    return $d
}

function script:XmlEsc([string]$s) { return [System.Security.SecurityElement]::Escape([string]$s) }

function script:DocRun([string]$t, [int]$sz, [bool]$bold, [string]$color) {
    $rpr = '<w:rPr><w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>'
    if ($bold) { $rpr += '<w:b/>' }
    if ($color) { $rpr += '<w:color w:val="' + $color + '"/>' }
    $rpr += '<w:sz w:val="' + $sz + '"/><w:szCs w:val="' + $sz + '"/></w:rPr>'
    return '<w:r>' + $rpr + '<w:t xml:space="preserve">' + (XmlEsc $t) + '</w:t></w:r>'
}

function script:DocPara([string]$jc, [string]$runs, [int]$sz) {
    $ppr = '<w:pPr>'
    if ($jc) { $ppr += '<w:jc w:val="' + $jc + '"/>' }
    $ppr += '<w:spacing w:after="40" w:line="240" w:lineRule="auto"/>'
    $ppr += '<w:rPr><w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/><w:sz w:val="' + $sz + '"/><w:szCs w:val="' + $sz + '"/></w:rPr></w:pPr>'
    return '<w:p>' + $ppr + $runs + '</w:p>'
}

function script:DocCell([string]$text, [int]$wdt, [bool]$bold, [string]$jc, [string]$color, [string]$shade) {
    $tc = '<w:tc><w:tcPr><w:tcW w:w="' + $wdt + '" w:type="dxa"/>'
    if ($shade) { $tc += '<w:shd w:val="clear" w:color="auto" w:fill="' + $shade + '"/>' }
    $tc += '<w:vAlign w:val="center"/></w:tcPr>'
    $tc += (DocPara $jc (DocRun $text 18 $bold $color) 18)
    $tc += '</w:tc>'
    return $tc
}

# $Content - an array of hashtables:
#  @{t='title';x='...'} @{t='sub';x='...'} @{t='meta';x='...'} @{t='space'}
#  @{t='head';x='...'} @{t='para';x='...';c='RRGGBB'(optional)}
#  @{t='table';cols=@(@{x='No.';w=600},...); rows=@(@(cell,...),...)}  cell = a string or @{x;c;b}
function New-DocxReport([string]$Path, [array]$Content) {
    $body = New-Object System.Text.StringBuilder
    foreach ($it in $Content) {
        switch ($it.t) {
            'title' { [void]$body.Append((DocPara 'center' (DocRun $it.x 32 $true '') 32)) }
            'sub'   { [void]$body.Append((DocPara 'center' (DocRun $it.x 26 $true '1F3864') 26)) }
            'meta'  { [void]$body.Append((DocPara $null (DocRun $it.x 22 $false '') 22)) }
            'space' { [void]$body.Append((DocPara $null '' 16)) }
            'head'  { [void]$body.Append((DocPara $null (DocRun $it.x 24 $true '1F3864') 24)) }
            'para'  { $col = if ($it.ContainsKey('c')) { $it.c } else { '' }; [void]$body.Append((DocPara $null (DocRun $it.x 20 $false $col) 20)) }
            'bar'   {
                # Horizontal bar chart: label + percentage + segmented fill.
                $pct = [int]$it.pct; if ($pct -lt 0) { $pct = 0 }; if ($pct -gt 100) { $pct = 100 }
                $color = if ($it.ContainsKey('color')) { $it.color } else { '2F9E44' }
                $runs = (DocRun "$($it.label)" 20 $true '') + (DocRun ('     ' + $pct + '%') 20 $true $color)
                [void]$body.Append((DocPara $null $runs 20))
                $segs = 20; $fill = [int][math]::Round($pct / 100 * $segs)
                $t = New-Object System.Text.StringBuilder
                [void]$t.Append('<w:tbl><w:tblPr><w:tblW w:w="9400" w:type="dxa"/><w:tblLayout w:type="fixed"/><w:tblBorders>')
                foreach ($e in 'top', 'left', 'bottom', 'right') { [void]$t.Append('<w:' + $e + ' w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>') }
                foreach ($e in 'insideH', 'insideV') { [void]$t.Append('<w:' + $e + ' w:val="none" w:sz="0" w:space="0" w:color="auto"/>') }
                [void]$t.Append('</w:tblBorders></w:tblPr><w:tblGrid>')
                for ($i = 0; $i -lt $segs; $i++) { [void]$t.Append('<w:gridCol w:w="470"/>') }
                [void]$t.Append('</w:tblGrid><w:tr>')
                for ($i = 0; $i -lt $segs; $i++) { $sh = if ($i -lt $fill) { $color } else { 'E2E8F0' }; [void]$t.Append((DocCell ' ' 470 $false 'center' '' $sh)) }
                [void]$t.Append('</w:tr></w:tbl>')
                [void]$body.Append($t.ToString())
                [void]$body.Append((DocPara $null '' 8))
            }
            'mono'  {
                $r = '<w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr><w:t xml:space="preserve">' + (XmlEsc $it.x) + '</w:t></w:r>'
                [void]$body.Append('<w:p><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr></w:pPr>' + $r + '</w:p>')
            }
            'table' {
                $cols = $it.cols
                $tbl = New-Object System.Text.StringBuilder
                [void]$tbl.Append('<w:tbl><w:tblPr><w:tblW w:w="0" w:type="auto"/><w:tblLayout w:type="fixed"/><w:tblBorders>')
                foreach ($e in 'top', 'left', 'bottom', 'right', 'insideH', 'insideV') { [void]$tbl.Append('<w:' + $e + ' w:val="single" w:sz="4" w:space="0" w:color="808080"/>') }
                [void]$tbl.Append('</w:tblBorders></w:tblPr><w:tblGrid>')
                foreach ($c in $cols) { [void]$tbl.Append('<w:gridCol w:w="' + $c.w + '"/>') }
                [void]$tbl.Append('</w:tblGrid>')
                # header row
                [void]$tbl.Append('<w:tr><w:trPr><w:tblHeader/></w:trPr>')
                foreach ($c in $cols) { [void]$tbl.Append((DocCell $c.x $c.w $true 'center' '' 'D9E1F2')) }
                [void]$tbl.Append('</w:tr>')
                foreach ($row in $it.rows) {
                    [void]$tbl.Append('<w:tr>')
                    for ($ci = 0; $ci -lt $cols.Count; $ci++) {
                        $cell = $row[$ci]
                        $txt = ''; $col = ''; $bold = $false; $jc = 'left'
                        if ($cell -is [hashtable]) { $txt = "$($cell.x)"; if ($cell.ContainsKey('c')) { $col = $cell.c }; if ($cell.ContainsKey('b')) { $bold = [bool]$cell.b }; if ($cell.ContainsKey('j')) { $jc = $cell.j } }
                        else { $txt = "$cell" }
                        [void]$tbl.Append((DocCell $txt $cols[$ci].w $bold $jc $col ''))
                    }
                    [void]$tbl.Append('</w:tr>')
                }
                [void]$tbl.Append('</w:tbl>')
                [void]$body.Append($tbl.ToString())
            }
        }
    }
    $doc = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
        '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + $body.ToString() +
        '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1417" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr>' +
        '</w:body></w:document>'
    $ct = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/>' +
        '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'
    $rels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'

    Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $enc = New-Object System.Text.UTF8Encoding $false
    $tmp = $Path + '.tmp'
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    $fs = [System.IO.File]::Create($tmp)
    $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    function AddZ($z, $name, $text, $enc) {
        $e = $z.CreateEntry($name)
        $s = $e.Open(); $b = $enc.GetBytes($text); $s.Write($b, 0, $b.Length); $s.Dispose()
    }
    AddZ $zip '[Content_Types].xml' $ct $enc
    AddZ $zip '_rels/.rels' $rels $enc
    AddZ $zip 'word/document.xml' $doc $enc
    $zip.Dispose(); $fs.Dispose()
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue }
    [System.IO.File]::Move($tmp, $Path)
}

# Extracts paragraph text from a docx (to keep the manual sections on the next run)
function Read-DocxText([string]$Path) {
    $out = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $Path)) { return $out }
    try {
        Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        $fs = [System.IO.File]::OpenRead($Path)
        $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Read)
        $entry = $zip.GetEntry('word/document.xml')
        if ($entry) {
            $sr = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8)
            $xml = $sr.ReadToEnd(); $sr.Dispose()
            foreach ($m in [regex]::Matches($xml, '<w:p[ >].*?</w:p>')) {
                $p = $m.Value
                $sb = New-Object System.Text.StringBuilder
                foreach ($tm in [regex]::Matches($p, '<w:t[^>]*>(.*?)</w:t>')) { [void]$sb.Append($tm.Groups[1].Value) }
                $line = $sb.ToString()
                $line = $line -replace '&lt;', '<' -replace '&gt;', '>' -replace '&quot;', '"' -replace '&apos;', "'" -replace '&amp;', '&'
                $out.Add($line)
            }
        }
        $zip.Dispose(); $fs.Dispose()
    } catch {}
    return $out
}
