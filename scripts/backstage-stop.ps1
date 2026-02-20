Param(
  [switch]$Force
)

$ErrorActionPreference = "Stop"

# Default Backstage ports (frontend 3000, backend 7007)
$ports = @(3000, 7007)

Write-Host "Stopping Backstage (ports: $($ports -join ', '))..."

function Get-PidsByPort {
  param([int]$Port)

  try {
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
  } catch {
    $conns = @()
  }

  if (-not $conns) { return @() }

  return ($conns | Select-Object -ExpandProperty OwningProcess -Unique)
}

$pids = @()

foreach ($p in $ports) {
  $found = Get-PidsByPort -Port $p
  if ($found.Count -gt 0) {
    Write-Host "Found listener(s) on port ${p}: $($found -join ', ')"
    $pids += $found
  } else {
    Write-Host "No listener found on port ${p}."
  }
}

$pids = $pids | Sort-Object -Unique

if ($pids.Count -eq 0) {
  Write-Host "Nothing to stop."
  exit 0
}

# Try graceful stop first
foreach ($pid in $pids) {
  try {
    Write-Host "Stopping PID $pid ..."
    Stop-Process -Id $pid -ErrorAction SilentlyContinue
  } catch {}
}

Start-Sleep -Milliseconds 800

# If still alive, force kill (or if -Force specified)
$stillAlive = @()
foreach ($pid in $pids) {
  if (Get-Process -Id $pid -ErrorAction SilentlyContinue) {
    $stillAlive += $pid
  }
}

if ($stillAlive.Count -gt 0) {
  if ($Force) {
    foreach ($pid in $stillAlive) {
      Write-Host "Force stopping PID $pid ..."
      Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
    }
  } else {
    Write-Host "Some processes are still alive: $($stillAlive -join ', ')"
    Write-Host "Re-run with -Force to kill them:"
    Write-Host "  .\scripts\backstage-stop.ps1 -Force"
    exit 0
  }
}

Write-Host "Backstage stopped (if it was running)."
