# Run the harness selftests on Windows (PowerShell). Money: $0 at every level.
#   powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1            # offline only
#   powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 unity      # + Unity pipeline (editor found via the Unity CLI)
#   powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 live       # + real Claude on PLAN quota, Haiku forced
#   powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 all
# Output is also saved to selftest-results.txt — paste it back to Claude if anything is red.
param([string]$Level = "")
$ErrorActionPreference = "Continue"
Set-Location (Join-Path $PSScriptRoot "..")

$py = if (Get-Command py -ErrorAction SilentlyContinue) { "py" } else { "python" }
if (-not (Test-Path .venv)) { & $py -m venv .venv }
$vpy = ".\.venv\Scripts\python.exe"
& $vpy -m pip install -q --upgrade pip | Out-Null
& $vpy -m pip install -q -r harness\requirements.txt

$map = @{ "" = @(); "unity" = @("--unity"); "live" = @("--live"); "all" = @("--all") }
if (-not $map.ContainsKey($Level)) { Write-Host "unknown level: $Level"; exit 2 }

$env:PYTHONIOENCODING = "utf-8"
& {
  "# selftest $(Get-Date -Format 'yyyy-MM-dd HH:mm') level=$(if ($Level) { $Level } else { 'offline' })"
  "os=$([Environment]::OSVersion.VersionString) python=$(& $vpy --version)"
  foreach ($t in "git", "ffmpeg", "unity", "claude", "uvx") {
    $c = Get-Command $t -ErrorAction SilentlyContinue
    "$t=$(if ($c) { $c.Source } else { 'MISSING' })"
  }
  python -c "import sys; sys.path.insert(0, 'harness'); from core import *; c = load_config(); print('blender=' + str(find_blender(c) or 'MISSING')); print('unity_editor=' + str(find_unity(c, ROOT / c['game']['unity_project']) or 'MISSING'))"
  & $vpy harness\selftest.py @($map[$Level]) 2>&1
  "exit=$LASTEXITCODE"
} | Tee-Object -FilePath selftest-results.txt
