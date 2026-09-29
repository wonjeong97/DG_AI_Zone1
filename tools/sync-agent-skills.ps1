# Copies .agents/skills (source of truth) to .claude/skills.
# Usage: powershell -ExecutionPolicy Bypass -File tools/sync-agent-skills.ps1 [-Check]
#   -Check  Only report differences; exit 1 if the folders differ.
param([switch]$Check)

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root '.agents\skills'
$dst = Join-Path $root '.claude\skills'

function Get-FileHashes($dir) {
    $map = @{}
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($dir.Length + 1)
            $map[$rel] = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
        }
    }
    return $map
}

if ($Check) {
    $a = Get-FileHashes $src
    $b = Get-FileHashes $dst
    $diffs = @()
    foreach ($k in $a.Keys) {
        if (-not $b.ContainsKey($k)) { $diffs += "missing in .claude: $k" }
        elseif ($a[$k] -ne $b[$k]) { $diffs += "differs: $k" }
    }
    foreach ($k in $b.Keys) {
        if (-not $a.ContainsKey($k)) { $diffs += "extra in .claude: $k" }
    }
    if ($diffs.Count -gt 0) {
        $diffs | ForEach-Object { Write-Output $_ }
        exit 1
    }
    Write-Output 'Skills are in sync.'
    exit 0
}

if (Test-Path $dst) { Remove-Item -Path $dst -Recurse -Force }
Copy-Item -Path $src -Destination $dst -Recurse
Write-Output 'Synced .agents/skills -> .claude/skills'
