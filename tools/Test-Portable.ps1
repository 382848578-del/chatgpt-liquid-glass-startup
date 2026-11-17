$ErrorActionPreference='Stop'
$taskPackage=Split-Path -Parent $PSScriptRoot
$taskTestRoot=Join-Path ([IO.Path]::GetTempPath()) ('ChatGPTGlass-Test-'+[guid]::NewGuid().ToString('N'))
$taskInstall=Join-Path $taskTestRoot 'Installed App'
$taskDesktop=Join-Path $taskTestRoot 'Desktop'
New-Item -ItemType Directory -Force -Path $taskTestRoot,$taskDesktop | Out-Null
$taskOriginal=Join-Path $taskDesktop 'ChatGPT.lnk'
$taskShell=New-Object -ComObject WScript.Shell
$taskLink=$taskShell.CreateShortcut($taskOriginal)
$taskLink.TargetPath=Join-Path $env:WINDIR 'System32\notepad.exe'
$taskLink.Description='Original shortcut fixture'
$taskLink.Save()
$taskHash=(Get-FileHash -LiteralPath $taskOriginal).Hash
$taskAppFixture=Join-Path $taskTestRoot 'Original ChatGPT.lnk'
Copy-Item -LiteralPath $taskOriginal -Destination $taskAppFixture -Force
& (Join-Path $taskPackage 'Install.ps1') -ShortcutPath $taskAppFixture -InstallDirectory $taskInstall -DesktopDirectory $taskDesktop -NonInteractive
$taskInstalledLink=$taskShell.CreateShortcut($taskOriginal)
if ($taskInstalledLink.TargetPath -ne (Join-Path $taskInstall 'ChatGPT-Startup.exe')) { throw 'Installer did not connect the shortcut.' }
if ((Get-FileHash -LiteralPath (Join-Path $taskInstall 'desktop-original.lnk')).Hash -ne $taskHash) { throw 'Original shortcut backup mismatch.' }
$taskConfig=Get-Content -LiteralPath (Join-Path $taskInstall 'app-config.json') -Raw | ConvertFrom-Json
if ($taskConfig.ProcessNames[0] -ne 'notepad') { throw 'Application process detection failed.' }
$taskRun=Start-Process -FilePath (Join-Path $taskInstall 'ChatGPT-Startup.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
$taskRun.WaitForExit()
if ($taskRun.ExitCode -ne 0) { throw ('Native self-test failed: '+$taskRun.ExitCode) }
$taskNative=Get-Content -LiteralPath (Join-Path $taskInstall 'launcher-self-test.json') -Raw | ConvertFrom-Json
if (-not $taskNative.BlackFirstFrame -or $taskNative.Reason -ne 'complete' -or $taskNative.WindowRebinds -lt 2) { throw 'Native animation regression.' }
& (Join-Path $taskPackage 'Install.ps1') -InstallDirectory $taskInstall -DesktopDirectory $taskDesktop -NonInteractive
& (Join-Path $taskPackage 'Uninstall.ps1') -InstallDirectory $taskInstall
if ((Get-FileHash -LiteralPath $taskOriginal).Hash -ne $taskHash) { throw 'Uninstall did not restore the exact original shortcut.' }
if (Test-Path -LiteralPath (Join-Path $taskInstall 'ChatGPT-Startup.exe')) { throw 'Uninstall left executable behind.' }
$taskFresh=Join-Path $taskTestRoot 'Fresh Desktop'
$taskFreshInstall=Join-Path $taskTestRoot 'Fresh Install'
& (Join-Path $taskPackage 'Install.ps1') -ShortcutPath (Join-Path $env:WINDIR 'System32\notepad.exe') -InstallDirectory $taskFreshInstall -DesktopDirectory $taskFresh -NonInteractive
& (Join-Path $taskPackage 'Uninstall.ps1') -InstallDirectory $taskFreshInstall
if (Test-Path -LiteralPath (Join-Path $taskFresh 'ChatGPT.lnk')) { throw 'Uninstall left a newly-created shortcut behind.' }
Write-Output 'PASS: portable install, independent app detection, native black-first-frame/window-replacement playback, reinstall, exact shortcut restore, clean uninstall, and first-install without a desktop shortcut.'
