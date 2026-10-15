$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$output\Fortress.exe" "$PSScriptRoot\Fortress.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built: $output\Fortress.exe"
