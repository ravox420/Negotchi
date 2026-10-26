param([string]$Package = 'pets-criados\bmo')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
$bmoAssemblyPath = (Resolve-Path -LiteralPath '.build\Negotchi.exe').Path
$bmoPackagePath = (Resolve-Path -LiteralPath $Package).Path
$bmoAssembly = [Reflection.Assembly]::LoadFrom($bmoAssemblyPath)
$bmoType = $bmoAssembly.GetType('Negotchi.PetPackages', $true)
$bmoMethod = $bmoType.GetMethod('LoadCreated', [Reflection.BindingFlags]'Public,Static')
$bmoAssets = $bmoMethod.Invoke($null, @($bmoPackagePath))
$bmoFlags = [Reflection.BindingFlags]'Public,Instance'
$bmoAssetsType = $bmoAssets.GetType()
$bmoFrames = $bmoAssetsType.GetField('Frames', $bmoFlags).GetValue($bmoAssets)
$bmoAnimations = $bmoAssetsType.GetField('Animations', $bmoFlags).GetValue($bmoAssets)
if ($bmoFrames.Length -ne 96 -or -not $bmoAnimations.ContainsKey('dancing')) { throw 'Pacote BMO incompleto no runtime.' }
$bmoDance = $bmoAnimations['dancing']
$bmoDanceFrames = $bmoDance.GetType().GetField('Frames', $bmoFlags).GetValue($bmoDance)
if ($bmoDanceFrames.Length -ne 8) { throw 'Danca precisa de oito quadros.' }
[pscustomobject]@{valid=$true; validator='Negotchi.PetPackages.LoadCreated'; frames=$bmoFrames.Length; animations=$bmoAnimations.Count; dancingFrames=$bmoDanceFrames.Length} | ConvertTo-Json
