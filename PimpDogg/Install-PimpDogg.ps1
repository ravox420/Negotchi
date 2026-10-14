$ErrorActionPreference = 'Stop'
$petSource = Join-Path $PSScriptRoot 'package-transparent'
$petTarget = 'C:\Users\DIRECTA\.codex\pets\PimpDogg'
if (Test-Path -LiteralPath $petTarget) {
    throw "O destino já existe; instalação interrompida para preservar os arquivos: $petTarget"
}
foreach ($petFile in @('pet.json', 'spritesheet.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $petSource $petFile))) {
        throw "Arquivo do pacote ausente: $petFile"
    }
}
New-Item -ItemType Directory -Path $petTarget | Out-Null
foreach ($petFile in @('pet.json', 'spritesheet.png')) {
    Copy-Item -LiteralPath (Join-Path $petSource $petFile) -Destination (Join-Path $petTarget $petFile)
    $petExpected = (Get-FileHash -LiteralPath (Join-Path $petSource $petFile)).Hash
    $petActual = (Get-FileHash -LiteralPath (Join-Path $petTarget $petFile)).Hash
    if ($petExpected -ne $petActual) { throw "Falha na verificação de $petFile" }
}
Write-Output 'PimpDogg instalado e verificado. Seletor: custom:PimpDogg'
