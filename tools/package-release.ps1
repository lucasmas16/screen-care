param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\dist'), [string]$Version = '1.5.8')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versão inválida.' }
$build = [IO.Path]::GetFullPath($BuildDirectory)
$identity = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $build 'ScreenCare.exe'))
if ($identity.Name -ne 'ScreenCare' -or $identity.Version.ToString(3) -ne $Version) { throw 'Identidade/versão diferente da release.' }
$output = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\release\' + $Version)))
if ((Test-Path -LiteralPath $output) -and (Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw 'A pasta da release já contém arquivos. Preserve ou revise a release existente.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$portableNames = @('ScreenCare.exe', 'ScreenCare.exe.config', 'LEIA-ME.txt', 'LICENSE')
$portableFiles = @($portableNames | ForEach-Object { Join-Path $build $_ })
foreach ($name in @('ScreenCare-Setup.exe', 'ScreenCare-Setup.exe.config', 'LEIA-ME.txt', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $build $name) -Destination $output
}
$zip = Join-Path $output 'ScreenCare-Portatil.zip'
Compress-Archive -LiteralPath $portableFiles -DestinationPath $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $actualNames = @($archive.Entries | ForEach-Object FullName | Sort-Object)
    if (($actualNames -join '|') -ne (($portableNames | Sort-Object) -join '|')) { throw 'Conteúdo inesperado no ZIP.' }
    foreach ($entry in $archive.Entries) {
        $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath (Join-Path $build $entry.FullName)).Hash) { throw ('Hash diferente no ZIP: ' + $entry.FullName) }
    }
} finally { $archive.Dispose() }
$lines = @(Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
})
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), $lines, (New-Object Text.UTF8Encoding($false)))
Write-Output ('Release preparada: ' + $output)
Write-Output 'PASS: portable ZIP contains only the four approved files; all hashes match the build.'
