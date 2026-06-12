param(
    [Parameter(Mandatory = $true)]
    [string]$SourceCfg,
    [Parameter(Mandatory = $true)]
    [string]$OutputIni
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SourceCfg)) {
    Write-Error "Source cfg not found: $SourceCfg"
}

$lines = Get-Content -LiteralPath $SourceCfg -Encoding UTF8
$out = New-Object System.Collections.Generic.List[string]
$currentSection = $null

foreach ($raw in $lines) {
    $line = $raw.Trim()
    if ($line -match '^\[(.+)\]$') {
        $currentSection = $Matches[1].Trim()
        if ($out.Count -gt 0 -and $out[$out.Count - 1] -ne "") {
            $out.Add("")
        }
        $out.Add("[$currentSection]")
        continue
    }
    if ($line -match '^([^#=;\s][^=]*?)\s*=\s*(.+)$') {
        $key = $Matches[1].Trim()
        $val = $Matches[2].Trim()
        if ($key -and $currentSection) {
            $out.Add("$key = $val")
        }
    }
}

$dir = Split-Path -Parent $OutputIni
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

[System.IO.File]::WriteAllText($OutputIni, ($out -join [Environment]::NewLine) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $OutputIni ($($out.Count) lines)"
