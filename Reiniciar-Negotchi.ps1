[CmdletBinding()]
param([ValidateRange(0, 30)][int]$DelaySeconds = 0)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$executable = Join-Path $projectRoot '.build\Negotchi.exe'
$buildScript = Join-Path $projectRoot 'desktop\build.ps1'
$stagingDirectory = Join-Path $projectRoot '.artifacts\restart'

# Compile before closing the current instance so a build failure keeps it available.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript -Destination $stagingDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed; the current instance was kept running.' }
if ($DelaySeconds -gt 0) { Start-Sleep -Seconds $DelaySeconds }

foreach ($process in @(Get-Process -Name 'Negotchi' -ErrorAction SilentlyContinue)) {
    try { $processPath = $process.MainModule.FileName } catch { continue }
    if (-not [string]::Equals($processPath, $executable, [StringComparison]::OrdinalIgnoreCase)) { continue }
    Stop-Process -Id $process.Id -ErrorAction Stop
    $process.WaitForExit(5000) | Out-Null
    if (-not $process.HasExited) { throw 'O Negotchi não foi encerrado a tempo.' }
}

Copy-Item -LiteralPath (Join-Path $stagingDirectory 'Negotchi.exe') -Destination $executable -Force
if (-not (Test-Path -LiteralPath $executable)) { throw 'O executável do Negotchi não foi gerado.' }

Start-Process -FilePath $executable
