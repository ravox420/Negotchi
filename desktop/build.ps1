param([switch]$Run, [switch]$Test, [string]$Destination, [string[]]$TestRunArguments)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = if ($Destination) { [System.IO.Path]::GetFullPath($Destination) } else { Join-Path $projectRoot '.build' }
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $frameworkDirectory 'csc.exe'))) {
    $frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $frameworkDirectory 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Instale o .NET Framework 4.8 para compilar o Negotchi.' }
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Web.Extensions.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001', '/utf8output', '/warn:4', ('/out:' + (Join-Path $outputDirectory 'Negotchi.exe')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $frameworkDirectory $reference) }
foreach ($reference in @('PresentationCore.dll', 'PresentationFramework.dll', 'WindowsBase.dll')) {
    $arguments += '/reference:' + (Join-Path $frameworkDirectory ('WPF\' + $reference))
}
$runtimeFacade = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime') -Filter 'System.Runtime.dll' -Recurse | Select-Object -First 1
if (-not $runtimeFacade) { throw 'System.Runtime do .NET Framework nao encontrado.' }
$arguments += '/reference:' + $runtimeFacade.FullName
foreach ($metadata in @('Windows.Foundation.winmd', 'Windows.Storage.winmd', 'Windows.Data.winmd', 'Windows.Graphics.winmd', 'Windows.Media.winmd')) {
    $arguments += '/reference:' + (Join-Path $env:WINDIR ('System32\WinMetadata\' + $metadata))
}
foreach ($petDirectory in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'pets') -Directory | Sort-Object Name)) {
    foreach ($petFile in @('pet.json', 'spritesheet.png', 'preview.png')) {
        $arguments += '/resource:' + (Join-Path $petDirectory.FullName $petFile) + ',Negotchi.pets.' + $petDirectory.Name + '.' + $petFile
    }
}
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | ForEach-Object { $_.FullName })
if ($sourceFiles.Count -eq 0) { throw 'Nenhum arquivo C# encontrado em desktop/src.' }
$arguments += $sourceFiles
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'A compilacao do Negotchi falhou.' }
Write-Output ('Aplicativo: ' + (Join-Path $outputDirectory 'Negotchi.exe'))
$skillSource = Join-Path $projectRoot '.agents\skills\create-pet'
$skillDestination = Join-Path $outputDirectory 'skills'
New-Item -ItemType Directory -Force -Path $skillDestination | Out-Null
Copy-Item -LiteralPath $skillSource -Destination $skillDestination -Recurse -Force
if ($Test) {
    $testArguments = @('/nologo', '/target:exe', '/platform:anycpu', '/codepage:65001', '/utf8output', ('/out:' + (Join-Path $outputDirectory 'Negotchi.Tests.exe')), '/main:Negotchi.Tests')
    $testArguments += @($arguments | Where-Object { $_ -like '/reference:*' -or $_ -like '/resource:*' })
    $testArguments += $sourceFiles
    $testArguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -Recurse -File | Sort-Object FullName | ForEach-Object { $_.FullName })
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'A compilacao dos testes falhou.' }
    if ($TestRunArguments) { & (Join-Path $outputDirectory 'Negotchi.Tests.exe') @TestRunArguments }
    else {
        $testOutputDirectory = Join-Path (Join-Path $projectRoot '.artifacts\tests') ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        New-Item -ItemType Directory -Force -Path $testOutputDirectory | Out-Null
        & (Join-Path $outputDirectory 'Negotchi.Tests.exe') $testOutputDirectory
    }
    if ($LASTEXITCODE -ne 0) { throw 'Os testes do Negotchi falharam.' }
}
if ($Run) { Start-Process -FilePath (Join-Path $outputDirectory 'Negotchi.exe') -WindowStyle Normal }

