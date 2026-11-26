<#
U1-CHAR.4: run the production rig + animation QA on your own GLBs and write a before/after report.

  powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u4_local.ps1 `
      -Source "B:\Dravio\dravio_16_dragons_3d_pack\images" `
      -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
      [-OldWork "B:\Dravio\work_u3"] [-Work "B:\Dravio\work_u4"] [-UnityOut "Assets\Dravio\Character\Imported"] `
      [-Overrides "Assets\Dravio\Character\Data\asset-overrides.u4-local.json"] [-NoImages]

What it does (source GLBs are only read; nothing in -OldWork is changed):
  1. "before": if -OldWork (the work folder of your previous U1-CHAR.3 batch) is given, every
     <OldWork>\assets\<Name>\<Name>_Rigged.blend is measured again with the NEW anim_qa.py, same limits,
     into <Work>\qa_before. That is the rig you reported (Emberling/Sproutling PASS_WITH_WARNINGS).
  2. "after": batch_import.py with the v2 rig into <Work> (with --qa-images), using -Overrides.
     The default overrides set limbCount "auto" for droplet/zephyr/steamdrake: the rig is built only when
     the mesh measurement is unambiguous, otherwise the batch report says what was measured.
  3. copies the new <Name>_animqa.json/png into <Work>\qa_after and writes
     <Work>\U4_before_after.md (+ .json) with compare_animqa.py.
Send back: <Work>\U4_before_after.md, <Work>\batch-report.md, and <Work>\qa_after\*.png.
-UnityOut is optional: without it nothing is copied into the Unity project. Names listed in
<UnityOut>\approved-assets.json are never re-delivered.
#>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Blender,
    [string]$OldWork = "",
    [string]$Work = "",
    [string]$UnityOut = "",
    [string]$Overrides = "",
    [string]$Python = "python",
    [switch]$NoImages
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here "..\..\..")).Path
if (-not $Work) { $Work = Join-Path (Split-Path -Parent $Source) "work_u4" }
if (-not $Overrides) { $Overrides = Join-Path $repo "Assets\Dravio\Character\Data\asset-overrides.u4-local.json" }

if (-not (Test-Path $Source -PathType Container)) { throw "Source folder not found: $Source" }
$glbs = @(Get-ChildItem -Path $Source -Recurse -File | Where-Object { $_.Extension -ieq ".glb" })
if ($glbs.Count -eq 0) { throw "No .glb files in $Source (searched subfolders too)." }
if (-not (Test-Path $Blender -PathType Leaf)) { throw "Blender not found: $Blender" }
if ($OldWork -and ((Resolve-Path $OldWork).Path -eq [IO.Path]::GetFullPath($Work))) { throw "-Work must differ from -OldWork (the old results are kept as 'before')." }
Write-Host "Found $($glbs.Count) GLB file(s) in $Source"

New-Item -ItemType Directory -Force -Path $Work | Out-Null
$before = Join-Path $Work "qa_before"; $after = Join-Path $Work "qa_after"
New-Item -ItemType Directory -Force -Path $before, $after | Out-Null
$hashesBefore = @{}; foreach ($g in $glbs) { $hashesBefore[$g.FullName] = (Get-FileHash $g.FullName -Algorithm SHA256).Hash }

function Invoke-BlenderScript([string]$script, [string[]]$scriptArgs) {
    & $Blender --factory-startup --background --python-exit-code 1 --python (Join-Path $here $script) -- @scriptArgs
    return $LASTEXITCODE
}

$img = @(); if (-not $NoImages) { $img = @("--images") }

# 1. before: the previous rig measured with the new QA
if ($OldWork) {
    $old = @(Get-ChildItem -Path (Join-Path $OldWork "assets") -Recurse -Filter "*_Rigged.blend" -ErrorAction SilentlyContinue)
    if ($old.Count -eq 0) { Write-Warning "No *_Rigged.blend under $OldWork\assets: no 'before' measurement." }
    foreach ($b in $old) {
        $name = $b.BaseName -replace "_Rigged$", ""
        $rig = Join-Path $b.DirectoryName "${name}_rig.json"
        if (-not (Test-Path $rig)) { Write-Warning "Missing $rig, skipped"; continue }
        $qaArgs = @($b.FullName, $rig, $before, "--overwrite-outputs") + $img
        $lods = Join-Path $b.DirectoryName "${name}_lods.json"
        if (Test-Path $lods) { $qaArgs += @("--lods-json", $lods) }
        Write-Host "QA before: $name"
        if ((Invoke-BlenderScript "anim_qa.py" $qaArgs) -ne 0) { Write-Warning "anim_qa failed for $name (before)" }
    }
}

# 2. after: v2 batch
$batch = @((Join-Path $here "batch_import.py"), $Source, $Work, "--blender", $Blender, "--overrides", $Overrides)
if ($UnityOut) { $batch += @("--unity-out", $UnityOut) }
if (-not $NoImages) { $batch += "--qa-images" }
Write-Host "Batch v2: $Python $($batch -join ' ')"
& $Python @batch
$batchExit = $LASTEXITCODE

Get-ChildItem -Path (Join-Path $Work "assets") -Recurse -File -Include "*_animqa.json", "*_animqa.png" -ErrorAction SilentlyContinue |
    Copy-Item -Destination $after -Force

# 3. compare
$md = Join-Path $Work "U4_before_after.md"
& $Python (Join-Path $here "compare_animqa.py") $before $after $md --json (Join-Path $Work "U4_before_after.json") `
    --before-label "rig U1-CHAR.3" --after-label "rig v2 (U1-CHAR.4)"

foreach ($g in $glbs) {
    if ((Get-FileHash $g.FullName -Algorithm SHA256).Hash -ne $hashesBefore[$g.FullName]) { throw "Source GLB changed: $($g.FullName)" }
}
Write-Host ""
Write-Host "Source GLBs unchanged ($($glbs.Count) checked)."
Write-Host "Report:       $md"
Write-Host "Batch report: $(Join-Path $Work 'batch-report.md')"
Write-Host "QA images:    $after"
exit $batchExit
