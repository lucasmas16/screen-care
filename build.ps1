param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'), [switch]$RunTests)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Compilador do .NET Framework não encontrado.' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$common = @('/nologo', '/optimize+', '/platform:x64', '/codepage:65001', '/warnaserror+', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll', ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
$iconPath = Join-Path $PSScriptRoot 'ScreenCare.ico'
if (Test-Path -LiteralPath $iconPath) { $common += "/win32icon:$iconPath" }
$app = Join-Path $out 'ScreenCare.exe'
& $compiler @common '/target:winexe' '/main:ScreenCare.Program' "/out:$app" @sources
if ($LASTEXITCODE -ne 0) { throw 'Erro ao compilar aplicativo.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenCare.exe.config') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEIA-ME.txt') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $out -Force
$setup = Join-Path $out 'ScreenCare-Setup.exe'
$resources = @("/resource:$app,Payload.ScreenCare.exe", "/resource:$(Join-Path $out 'ScreenCare.exe.config'),Payload.ScreenCare.exe.config", "/resource:$(Join-Path $out 'LEIA-ME.txt'),Payload.LEIA-ME.txt")
$resources += "/resource:$(Join-Path $out 'LICENSE'),Payload.LICENSE"
& $compiler @common '/target:winexe' '/main:ScreenCare.SetupProgram' "/out:$setup" @resources @sources (Join-Path $PSScriptRoot 'setup\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Erro ao compilar instalador.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenCare.exe.config') -Destination ($setup + '.config') -Force
if ($RunTests) {
    $testExecutable = Join-Path $out 'ScreenCare.Tests.exe'
    & $compiler @common '/target:exe' '/main:ScreenCare.Tests' "/out:$testExecutable" @sources (Join-Path $PSScriptRoot 'setup\Setup.cs') (Join-Path $PSScriptRoot 'tests\Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Erro ao compilar testes.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ScreenCare.exe.config') -Destination ($testExecutable + '.config') -Force
    & $testExecutable (Join-Path $out 'test-results')
    if ($LASTEXITCODE -ne 0) { throw 'Um ou mais testes falharam.' }
}
Write-Output "Compilação concluída: $out"
