$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf=Join-Path $framework 'WPF'
$kit=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata'
$metadata=Get-ChildItem $kit -Filter Windows.winmd -Recurse | Where-Object {$_.Directory.Name -match '^10\.'} | Sort-Object FullName -Descending | Select-Object -First 1
if(-not $metadata){throw 'Install a Windows SDK with UnionMetadata first.'}
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll',"$framework\System.Runtime.dll","$framework\System.Threading.Tasks.dll","$framework\System.Runtime.WindowsRuntime.dll","$framework\System.Runtime.InteropServices.WindowsRuntime.dll","$wpf\PresentationFramework.dll","$wpf\PresentationCore.dll","$wpf\WindowsBase.dll",$metadata.FullName)|ForEach-Object {'/reference:'+$_}
& "$framework\csc.exe" /nologo /target:winexe "/win32icon:$PSScriptRoot\app\App.ico" "/out:$PSScriptRoot\app\SpotifyOverlay.exe" @refs "$PSScriptRoot\src\SpotifyOverlay.cs" "$PSScriptRoot\src\AlbumReader.cs"
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
Copy-Item "$PSScriptRoot\src\Overlay.xaml" "$PSScriptRoot\app\Overlay.xaml" -Force
Write-Output 'Built app/SpotifyOverlay.exe'

