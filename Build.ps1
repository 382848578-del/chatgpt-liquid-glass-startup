param([string]$OutputPath)
$ErrorActionPreference='Stop'
if (-not $OutputPath) { $OutputPath=Join-Path $PSScriptRoot 'bin\ChatGPT-Startup.exe' }
$taskFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskCompiler=Join-Path $taskFramework 'csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw '.NET Framework 4.x compiler is required.' }
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
$taskArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+$OutputPath),('/win32manifest:'+(Join-Path $PSScriptRoot 'src\startup.manifest')))
foreach ($taskRef in @('PresentationFramework','PresentationCore','WindowsBase')) { $taskArgs+=('/reference:'+(Join-Path $taskFramework ('WPF\'+$taskRef+'.dll'))) }
foreach ($taskRef in @('System.Xaml.dll','System.Drawing.dll','System.Web.Extensions.dll')) { $taskArgs+=('/reference:'+$taskRef) }
$taskArgs+=@((Join-Path $PSScriptRoot 'src\StartupVector.cs'),(Join-Path $PSScriptRoot 'src\StartupLauncher.cs'))
& $taskCompiler $taskArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host ('Built: '+$OutputPath)
