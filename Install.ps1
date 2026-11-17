param([string]$ShortcutPath,[string]$InstallDirectory,[string]$DesktopDirectory,[switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
$taskSource = $PSScriptRoot
if (-not $InstallDirectory) { $InstallDirectory = Join-Path $env:LOCALAPPDATA 'ChatGPTGlassStartup' }
if (-not $DesktopDirectory) { $DesktopDirectory = [Environment]::GetFolderPath('Desktop') }
$taskInstall = [IO.Path]::GetFullPath($InstallDirectory)
$taskDesktop = [IO.Path]::GetFullPath($DesktopDirectory)
$taskExe = Join-Path $taskInstall 'ChatGPT-Startup.exe'
$taskDesktopLink = Join-Path $taskDesktop 'ChatGPT.lnk'
$taskOriginal = Join-Path $taskInstall 'ChatGPT-original.lnk'
$taskShell = New-Object -ComObject WScript.Shell
$taskShellApp = New-Object -ComObject Shell.Application
function Test-OurLink([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    if ([IO.Path]::GetExtension($Path) -ne '.lnk') { return $false }
    $taskLink = $taskShell.CreateShortcut($Path)
    return ([IO.Path]::GetFileName($taskLink.TargetPath) -eq 'ChatGPT-Startup.exe') -or ($taskLink.Arguments -like '*Launch-ChatGPT.ps1*')
}
function Get-LinkAppId([string]$Path) {
    $taskFolder = $taskShellApp.NameSpace([IO.Path]::GetDirectoryName($Path))
    if ($null -eq $taskFolder) { return '' }
    $taskItem = $taskFolder.ParseName([IO.Path]::GetFileName($Path))
    return [string]$taskItem.ExtendedProperty('System.Link.TargetParsingPath')
}
if (-not (Test-Path -LiteralPath (Join-Path $taskSource 'bin\ChatGPT-Startup.exe'))) { throw 'The executable is missing. Extract the complete ZIP before installing.' }
if (-not [Environment]::Is64BitOperatingSystem) { throw '64-bit Windows is required.' }
$taskTargetShortcut = $null
$taskAppId = ''
if ($ShortcutPath) {
    $taskTargetShortcut = [IO.Path]::GetFullPath($ShortcutPath)
    if (-not (Test-Path -LiteralPath $taskTargetShortcut)) { throw 'The selected application or shortcut does not exist.' }
    if (Test-OurLink $taskTargetShortcut) { throw 'Select the original application shortcut, not the animation shortcut.' }
} elseif ((Test-Path -LiteralPath $taskOriginal) -and (Test-OurLink $taskDesktopLink)) {
    $taskTargetShortcut = $taskOriginal
} else {
    foreach ($taskCandidate in @($taskDesktopLink,(Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'ChatGPT.lnk'))) {
        if ((Test-Path -LiteralPath $taskCandidate) -and -not (Test-OurLink $taskCandidate)) { $taskTargetShortcut=$taskCandidate; break }
    }
    if (-not $taskTargetShortcut) {
        $taskApps = @(Get-StartApps -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^ChatGPT(\s|$)' })
        if ($taskApps.Count -gt 0) { $taskAppId = [string]$taskApps[0].AppID }
    }
    if (-not $taskTargetShortcut -and -not $taskAppId) {
        $taskRoots = @([Environment]::GetFolderPath('Programs'),[Environment]::GetFolderPath('CommonPrograms'))
        foreach ($taskRoot in $taskRoots) {
            $taskFound = Get-ChildItem -LiteralPath $taskRoot -Filter '*ChatGPT*.lnk' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($taskFound -and -not (Test-OurLink $taskFound.FullName)) { $taskTargetShortcut=$taskFound.FullName;break }
        }
    }
}
if (-not $taskTargetShortcut -and -not $taskAppId) {
    if ($NonInteractive) { throw 'ChatGPT was not detected. Install the desktop app or supply -ShortcutPath.' }
    Add-Type -AssemblyName System.Windows.Forms
    $taskPicker = New-Object System.Windows.Forms.OpenFileDialog
    $taskPicker.Title = 'Select the original ChatGPT shortcut or executable'
    $taskPicker.Filter = 'Application or shortcut (*.lnk;*.exe)|*.lnk;*.exe'
    $taskPicker.InitialDirectory = $taskDesktop
    if ($taskPicker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Installation cancelled. No shortcuts were changed.' }
    $taskTargetShortcut = $taskPicker.FileName
    if (Test-OurLink $taskTargetShortcut) { throw 'Select the original ChatGPT app.' }
}
New-Item -ItemType Directory -Force -Path $taskInstall,$taskDesktop | Out-Null
$taskStatePath=Join-Path $taskInstall 'install-state.json'
if (-not (Test-Path -LiteralPath $taskStatePath)) {
    $taskHadDesktop = (Test-Path -LiteralPath $taskDesktopLink) -and -not (Test-OurLink $taskDesktopLink)
    if ($taskHadDesktop) { Copy-Item -LiteralPath $taskDesktopLink -Destination (Join-Path $taskInstall 'desktop-original.lnk') -Force }
    [pscustomobject]@{DesktopDirectory=$taskDesktop;HadDesktopShortcut=$taskHadDesktop} | ConvertTo-Json | Set-Content -LiteralPath $taskStatePath -Encoding UTF8
}
if ($taskTargetShortcut -and [IO.Path]::GetExtension($taskTargetShortcut) -eq '.lnk') {
    if ([IO.Path]::GetFullPath($taskTargetShortcut) -ne $taskOriginal) { Copy-Item -LiteralPath $taskTargetShortcut -Destination $taskOriginal -Force }
    $taskAppId=Get-LinkAppId $taskOriginal
} else {
    $taskOriginalLink=$taskShell.CreateShortcut($taskOriginal)
    if ($taskAppId) {
        $taskOriginalLink.TargetPath=Join-Path $env:WINDIR 'explorer.exe'
        $taskOriginalLink.Arguments='shell:AppsFolder\'+$taskAppId
    } else { $taskOriginalLink.TargetPath=$taskTargetShortcut }
    $taskOriginalLink.Save()
}
$taskOriginalLink=$taskShell.CreateShortcut($taskOriginal)
$taskProcessName=[IO.Path]::GetFileNameWithoutExtension($taskOriginalLink.TargetPath)
$taskProcessNames=@('ChatGPT')
if ($taskProcessName -and $taskProcessName -notin @('explorer','powershell','cmd','ChatGPT')) { $taskProcessNames=@($taskProcessName) }
if ($taskAppId -like '*Codex*') { $taskProcessNames=@('ChatGPT','Codex') }
foreach ($taskProcess in @(Get-Process -Name 'ChatGPT-Startup' -ErrorAction SilentlyContinue)) {
    if ($taskProcess.Path -eq $taskExe -and -not $taskProcess.WaitForExit(12000)) { throw 'Close the running startup animation and retry.' }
}
Copy-Item -LiteralPath (Join-Path $taskSource 'bin\ChatGPT-Startup.exe') -Destination $taskExe -Force
Copy-Item -LiteralPath (Join-Path $taskSource 'startup-paths.json') -Destination (Join-Path $taskInstall 'startup-paths.json') -Force
Copy-Item -LiteralPath (Join-Path $taskSource 'Uninstall.ps1') -Destination (Join-Path $taskInstall 'Uninstall.ps1') -Force
Copy-Item -LiteralPath (Join-Path $taskSource 'Uninstall.cmd') -Destination (Join-Path $taskInstall 'Uninstall.cmd') -Force
[pscustomobject]@{ProcessNames=$taskProcessNames} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskInstall 'app-config.json') -Encoding UTF8
$taskIcon=$taskOriginalLink.IconLocation
if (-not $taskIcon -or $taskIcon -eq ',0') { $taskIcon=Join-Path $env:WINDIR 'System32\shell32.dll,220' }
$taskLink=$taskShell.CreateShortcut($taskDesktopLink)
$taskLink.TargetPath=$taskExe;$taskLink.Arguments='';$taskLink.WorkingDirectory=$taskInstall;$taskLink.WindowStyle=1
$taskLink.IconLocation=$taskIcon;$taskLink.Description='Welcome liu - Liquid Glass startup animation';$taskLink.Save()
Write-Host 'Installed. Open the ChatGPT desktop icon to use the animation.' -ForegroundColor Green
Write-Host ('Uninstall: '+(Join-Path $taskInstall 'Uninstall.cmd'))
