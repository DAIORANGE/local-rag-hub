# Builds the candidate GUI; never touches the running application, config, models or ports.
param([string]$OutputDirectory = "")
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $repo 'out'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $csc)){throw 'The .NET Framework 4.x C# compiler was not found.'}
if(!(Test-Path -LiteralPath (Join-Path $repo 'src\RequestMonitor.cs'))){throw 'RequestMonitor.cs missing.'}
if(!(Test-Path -LiteralPath (Join-Path $repo 'src\ModelProcessOwner.cs'))){throw 'ModelProcessOwner.cs missing.'}
New-Item -ItemType Directory -Force -Path $OutputDirectory|Out-Null
$target=Join-Path $OutputDirectory 'LocalRagHub.exe'
& $csc /nologo /target:winexe /optimize+ ("/out:"+$target) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /r:System.Web.Extensions.dll (Join-Path $repo 'src\LocalRagHub.cs') (Join-Path $repo 'src\RequestMonitor.cs') (Join-Path $repo 'src\ModelProcessOwner.cs')
if($LASTEXITCODE -ne 0){throw "Compile failed: $LASTEXITCODE"}
$h=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
Write-Output ("BUILD=PASS EXE="+$target+" SHA256="+$h)
