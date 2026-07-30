param(
    [string]$BattleTechRoot = ""
)

$ErrorActionPreference = "Stop"

$ModDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($BattleTechRoot)) {
    $ModsDirectory = Split-Path -Parent $ModDirectory
    $BattleTechRoot = Split-Path -Parent $ModsDirectory
}

$BattleTechRoot = [System.IO.Path]::GetFullPath($BattleTechRoot)
$ManagedDirectory = Join-Path $BattleTechRoot "BattleTech_Data\Managed"
$NewtonsoftPath = Join-Path $ManagedDirectory "Newtonsoft.Json.dll"
$HarmonyPath = Join-Path $BattleTechRoot "Mods\ModTek\lib\0Harmony.dll"
$UnityEnginePath = Join-Path $ManagedDirectory "UnityEngine.dll"
$UnityCorePath = Join-Path $ManagedDirectory "UnityEngine.CoreModule.dll"
$UnityUiPath = Join-Path $ManagedDirectory "UnityEngine.UI.dll"
$UnityUiModulePath = Join-Path $ManagedDirectory "UnityEngine.UIModule.dll"
$UnityTextRenderingPath = Join-Path $ManagedDirectory "UnityEngine.TextRenderingModule.dll"
$SourceDirectory = Join-Path $ModDirectory "Source"
$SourcePaths = @(Get-ChildItem -LiteralPath $SourceDirectory -Filter *.cs -File | ForEach-Object { $_.FullName })
$OutputDll = Join-Path $ModDirectory "TAMPS.dll"
$OutputPdb = Join-Path $ModDirectory "TAMPS.pdb"

$RequiredFiles = @(
    $NewtonsoftPath,
    $HarmonyPath,
    $UnityEnginePath,
    $UnityCorePath,
    $UnityUiPath,
    $UnityUiModulePath,
    $UnityTextRenderingPath
)

foreach ($RequiredFile in $RequiredFiles) {
    if (-not (Test-Path $RequiredFile)) {
        throw "Required assembly was not found at: $RequiredFile"
    }
}

if ($SourcePaths.Count -eq 0) {
    throw "No C# source files were found under: $SourceDirectory"
}

$CscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$CscPath = $null

foreach ($Candidate in $CscCandidates) {
    if (Test-Path $Candidate) {
        $CscPath = $Candidate
        break
    }
}

if ($null -eq $CscPath) {
    throw "The .NET Framework C# compiler (csc.exe) was not found."
}

if (Test-Path $OutputDll) {
    Remove-Item $OutputDll -Force
}

if (Test-Path $OutputPdb) {
    Remove-Item $OutputPdb -Force
}

Write-Host "BattleTech root: $BattleTechRoot"
Write-Host "Compiler: $CscPath"
Write-Host "Newtonsoft.Json: $NewtonsoftPath"
Write-Host "Harmony: $HarmonyPath"
Write-Host "Unity UI: $UnityUiPath"
Write-Host "Unity Text Rendering: $UnityTextRenderingPath"
Write-Host "Source files: $($SourcePaths.Count)"
Write-Host "Building: $OutputDll"

& $CscPath `
    /nologo `
    /target:library `
    /optimize+ `
    /debug:pdbonly `
    "/out:$OutputDll" `
    "/reference:$NewtonsoftPath" `
    "/reference:$HarmonyPath" `
    "/reference:$UnityEnginePath" `
    "/reference:$UnityCorePath" `
    "/reference:$UnityUiPath" `
    "/reference:$UnityUiModulePath" `
    "/reference:$UnityTextRenderingPath" `
    $SourcePaths

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $OutputDll)) {
    throw "Compilation finished without creating TAMPS.dll."
}

Write-Host ""
Write-Host "Build succeeded." -ForegroundColor Green
Write-Host "Start BATTLETECH, then inspect:"
Write-Host "  $ModDirectory\TAMPS.log"
Write-Host "Optional diagnostic file when WriteAllowList=true:"
Write-Host "  $ModDirectory\AmmoBoxAllowList.json"
