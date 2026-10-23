param([switch]$Run, [switch]$Test, [string]$Destination)
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
$references = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'System.Web.Extensions.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/warn:4', ('/out:' + (Join-Path $outputDirectory 'Negotchi.exe')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $frameworkDirectory $reference) }
foreach ($reference in @('PresentationCore.dll', 'PresentationFramework.dll', 'WindowsBase.dll')) {
    $arguments += '/reference:' + (Join-Path $frameworkDirectory ('WPF\' + $reference))
}
$pimpDoggDirectory = Join-Path $PSScriptRoot 'pets\PimpDogg'
$arguments += '/resource:' + (Join-Path $pimpDoggDirectory 'spritesheet.png') + ',Negotchi.spritesheet.png'
$arguments += '/resource:' + (Join-Path $pimpDoggDirectory 'pet.json') + ',Negotchi.pet.json'
$arguments += '/resource:' + (Join-Path $pimpDoggDirectory 'preview.png') + ',Negotchi.preview.png'
foreach ($petDirectory in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'pets') -Directory | Sort-Object Name)) {
    foreach ($petFile in @('pet.json', 'spritesheet.png', 'preview.png')) {
        $arguments += '/resource:' + (Join-Path $petDirectory.FullName $petFile) + ',Negotchi.pets.' + $petDirectory.Name + '.' + $petFile
    }
}
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'A compilacao do Negotchi falhou.' }
Write-Output ('Aplicativo: ' + (Join-Path $outputDirectory 'Negotchi.exe'))
if ($Test) {
    $testArguments = @('/nologo', '/target:exe', '/platform:anycpu', '/utf8output', ('/out:' + (Join-Path $outputDirectory 'Negotchi.Tests.exe')), '/main:Negotchi.Tests')
    $testArguments += @($arguments | Where-Object { $_ -like '/reference:*' -or $_ -like '/resource:*' })
    $testArguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
    $testArguments += Join-Path $PSScriptRoot 'tests\Tests.cs'
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'A compilacao dos testes falhou.' }
    & (Join-Path $outputDirectory 'Negotchi.Tests.exe') $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Os testes do Negotchi falharam.' }
}
if ($Run) { Start-Process -FilePath (Join-Path $outputDirectory 'Negotchi.exe') -WindowStyle Normal }

