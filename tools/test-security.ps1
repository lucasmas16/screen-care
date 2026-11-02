param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\dist'))
$ErrorActionPreference = 'Stop'
$build = [IO.Path]::GetFullPath($BuildDirectory)
$tests = Join-Path $build 'ScreenCare.Tests.exe'
if (-not (Test-Path -LiteralPath $tests)) { throw 'Compile primeiro com -RunTests.' }
$root = Join-Path $build ('security-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$target = Join-Path $root 'target'
$link = Join-Path $root 'redirect'
New-Item -ItemType Directory -Path $target | Out-Null
[IO.File]::WriteAllText((Join-Path $target 'sentinel.txt'), 'synthetic; must remain unchanged')
$before = (Get-FileHash -LiteralPath (Join-Path $target 'sentinel.txt')).Hash
& $tests --check-path (Join-Path $root 'ordinary\new-install')
if ($LASTEXITCODE -ne 0) { throw 'Um destino normal foi rejeitado.' }
New-Item -ItemType Junction -Path $link -Target $target | Out-Null
try {
    & $tests --check-path $link
    if ($LASTEXITCODE -ne 2) { throw 'Junction no destino não foi rejeitada.' }
    & $tests --check-path (Join-Path $link 'new-install')
    if ($LASTEXITCODE -ne 2) { throw 'Junction ancestral não foi rejeitada.' }
    if ((Get-FileHash -LiteralPath (Join-Path $target 'sentinel.txt')).Hash -ne $before) { throw 'O arquivo externo foi alterado.' }
} finally {
    # Delete only this junction, never recursively and never its target.
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($link)) -ne $root) { throw 'Destino fora da pasta de teste.' }
    if ((Get-Item -LiteralPath $link -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { [IO.Directory]::Delete($link) }
}
Write-Output 'PASS: normal path accepted; direct and ancestor junctions rejected; target preserved.'
$probe = Join-Path $root 'dll-probe'
New-Item -ItemType Directory -Path $probe | Out-Null
Copy-Item -LiteralPath $tests -Destination $probe
Copy-Item -LiteralPath ($tests + '.config') -Destination $probe
# Invalid, inert files exercise the loader without using executable attack code.
foreach ($name in @('xinput1_4.dll', 'winmm.dll', 'shcore.dll')) { [IO.File]::WriteAllText((Join-Path $probe $name), 'not a library; synthetic loader test') }
Push-Location $probe
try {
    & (Join-Path $probe 'ScreenCare.Tests.exe') --check-native-dlls
    if ($LASTEXITCODE -ne 0) { throw 'A busca segura de DLLs falhou.' }
} finally { Pop-Location }
Write-Output 'PASS: local DLL decoys ignored; Windows native libraries used.'
