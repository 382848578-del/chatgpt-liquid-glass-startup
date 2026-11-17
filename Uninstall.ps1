param([string]$InstallDirectory)
$ErrorActionPreference='Stop'
if (-not $InstallDirectory) { $InstallDirectory=$PSScriptRoot }
$taskInstall=[IO.Path]::GetFullPath($InstallDirectory)
$taskStatePath=Join-Path $taskInstall 'install-state.json'
if (-not (Test-Path -LiteralPath $taskStatePath)) { throw 'Installation state was not found. Run this script from the installed folder.' }
$taskState=Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
$taskDesktopLink=Join-Path $taskState.DesktopDirectory 'ChatGPT.lnk'
$taskExe=Join-Path $taskInstall 'ChatGPT-Startup.exe'
$taskShell=New-Object -ComObject WScript.Shell
$taskOurLink=$false
if (Test-Path -LiteralPath $taskDesktopLink) { $taskOurLink=$taskShell.CreateShortcut($taskDesktopLink).TargetPath -eq $taskExe }
if ($taskOurLink) {
    if ($taskState.HadDesktopShortcut -and (Test-Path -LiteralPath (Join-Path $taskInstall 'desktop-original.lnk'))) {
        Copy-Item -LiteralPath (Join-Path $taskInstall 'desktop-original.lnk') -Destination $taskDesktopLink -Force
    } else { Remove-Item -LiteralPath $taskDesktopLink }
}
foreach ($taskProcess in @(Get-Process -Name 'ChatGPT-Startup' -ErrorAction SilentlyContinue)) {
    if ($taskProcess.Path -eq $taskExe -and -not $taskProcess.WaitForExit(12000)) { throw 'Close the startup animation before uninstalling.' }
}
# Delete only explicitly named files in the verified installation directory; no recursive deletion.
foreach ($taskName in @('ChatGPT-Startup.exe','startup-paths.json','app-config.json','ChatGPT-original.lnk','desktop-original.lnk','last-run.json','window-placement.json','launcher-error.log','launcher-self-test.json','install-state.json','Uninstall.cmd','Uninstall.ps1')) {
    $taskPath=[IO.Path]::GetFullPath((Join-Path $taskInstall $taskName))
    if ([IO.Path]::GetDirectoryName($taskPath) -ne $taskInstall.TrimEnd('\')) { throw 'Unsafe uninstall path.' }
    if (Test-Path -LiteralPath $taskPath) { Remove-Item -LiteralPath $taskPath -Force }
}
Write-Host 'Uninstalled. The original desktop shortcut has been restored when available.' -ForegroundColor Green
