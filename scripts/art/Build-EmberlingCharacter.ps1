[CmdletBinding()]
param(
    [string]$SourceGlb = "art-exports/EmberlingRaw.glb",
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe",
    [string]$Python = "python",
    [ValidateSet(256,512,1024,2048)][int]$TextureSize = 1024,
    [double]$YawOffset = 0,
    [switch]$Winged,
    [switch]$InstallLods
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Push-Location $root
try {
    if (-not (Test-Path -LiteralPath $Blender -PathType Leaf)) { throw "Blender not found: $Blender" }
    $source = (Resolve-Path -LiteralPath $SourceGlb).Path
    if ([IO.Path]::GetExtension($source) -ine ".glb") { throw "Source must be a .glb file" }
    if ([double]::IsNaN($YawOffset) -or [double]::IsInfinity($YawOffset)) { throw "Yaw must be finite" }
    Get-Command $Python -ErrorAction Stop | Out-Null
    $before = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $name = "DRG_emberling_Baby"
    $run = Join-Path $root ("art-exports/character-runs/" + (Get-Date -Format "yyyyMMdd_HHmmss") + "_" + [guid]::NewGuid().ToString("N").Substring(0,8))
    $prep = Join-Path $run "prep"
    $rig = Join-Path $run "rig"
    $lod = Join-Path $run "lod"
    foreach ($folder in @($run,$prep,$rig,$lod)) { New-Item -ItemType Directory -Path $folder -ErrorAction Stop | Out-Null }
    function Invoke-BlenderStep([string]$Script, [string[]]$StepArgs) {
        Write-Host "=== $Script ==="
        & $Blender --factory-startup --background --python-exit-code 1 --python $Script -- @StepArgs
        if ($LASTEXITCODE -ne 0) { throw "Blender failed ($LASTEXITCODE): $Script. Reports: $run" }
    }
    function Require-File([string]$File) {
        if (-not (Test-Path -LiteralPath $File -PathType Leaf) -or (Get-Item -LiteralPath $File).Length -le 0) {
            throw "Missing/empty expected output: $File"
        }
    }
    $sourceReport = Join-Path $run "source.inspect.json"
    Invoke-BlenderStep "scripts/art/character/inspect_character.py" @($source,$sourceReport)
    Require-File $sourceReport
    Invoke-BlenderStep "scripts/art/character/prepare_character.py" @($source,$prep,"--name",$name,"--texture-size",[string]$TextureSize)
    $prepared = Join-Path $prep ($name+"_Prepared.glb")
    $preparedReport = Join-Path $prep ($name+"_prepare.json")
    Require-File $prepared
    Require-File $preparedReport
    $prepInfo = Get-Content -LiteralPath $preparedReport -Raw | ConvertFrom-Json
    if (-not $prepInfo.source_unchanged) { throw "Preparation did not confirm original source unchanged" }
    if ($prepInfo.base_color_source -eq "constant") {
        Write-Warning "Source color is constant. UV creation cannot reconstruct missing artwork."
    }
    $fbx = Join-Path $rig "EmberlingRefinedGLB.fbx"
    $blend = [IO.Path]::ChangeExtension($fbx,".blend")
    $yaw = $YawOffset.ToString([Globalization.CultureInfo]::InvariantCulture)
    $rigArgs = @($fbx,"--source-glb",$prepared,"--yaw-offset",$yaw)
    if ($Winged) { $rigArgs += "--winged" }
    Invoke-BlenderStep "scripts/art/build_emberling_living.py" $rigArgs
    Require-File $fbx
    Require-File $blend
    Invoke-BlenderStep "scripts/art/character/build_lods.py" @($blend,$lod,"--name",$name)
    $lodFbx = Join-Path $lod ($name+"_LODs.fbx")
    $lodReport = Join-Path $lod ($name+"_lods.json")
    Require-File $lodFbx
    Require-File $lodReport
    $inspect = Join-Path $run "lod.inspect.json"
    $validation = Join-Path $run "validation.json"
    Invoke-BlenderStep "scripts/art/character/inspect_character.py" @($lodFbx,$inspect)
    Require-File $inspect
    & $Python "scripts/art/character/validate_character.py" --inspect $inspect --lods $lodReport --prepare $preparedReport --out $validation
    if ($LASTEXITCODE -ne 0) { throw "Character validation failed. Review $validation; nothing copied into Unity." }
    Require-File $validation
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $before) { throw "Original GLB changed; refusing installation" }
    $report = Get-Content -LiteralPath $validation -Raw | ConvertFrom-Json
    if ($report.status -notin @("PASS","PASS_WITH_WARNINGS")) { throw "Unexpected validation status: $($report.status)" }
    Write-Host "Validation: $($report.status). Review reports and appearance before use."
    if ($InstallLods) {
        # Stage separately from the existing EmberlingRefinedGLB.fbx; do not replace scene assets.
        $modelDir = Join-Path $root "Assets/Dravio/Character/Models"
        $textureDir = Join-Path $root "Assets/Dravio/Character/Textures"
        $copies = @(@{Source=$lodFbx;Destination=(Join-Path $modelDir ([IO.Path]::GetFileName($lodFbx)))})
        foreach ($suffix in @("_BaseColor.png","_Normal.png","_MetallicSmoothness.png")) {
            $file = Join-Path $prep ($name+$suffix)
            if (Test-Path -LiteralPath $file -PathType Leaf) {
                $copies += @{Source=$file;Destination=(Join-Path $textureDir ([IO.Path]::GetFileName($file)))}
            }
        }
        foreach ($copy in $copies) {
            if ((Test-Path -LiteralPath $copy.Destination) -or (Test-Path -LiteralPath ($copy.Destination+".meta"))) {
                throw "Destination or meta already exists: $($copy.Destination). Back up/review manually; no overwrites."
            }
        }
        foreach ($folder in @($modelDir,$textureDir)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
        foreach ($copy in $copies) { Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination -ErrorAction Stop }
        Write-Host "Staged LOD FBX/textures in Assets/Dravio/Character. Use Character material/LOD prefab tools; Open Game still uses existing preview FBX."
    }
    Write-Host "Output folder: $run"
    Write-Host "Source unchanged: $before"
}
finally { Pop-Location }
