<#
U1-CHAR.7: advanced creature motion (water, air, perch, burrow, greet) on YOUR 7 GLBs (01_emberling .. 07_sparkwing).

  powershell -ExecutionPolicy Bypass -File scripts\art\character\run_u7_local.ps1 `
      -Source "B:\Dravio\dravio_16_dragons_3d_pack\images" `
      -Blender "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" `
      [-OldWork "B:\Dravio\work_u6"] [-Work "B:\Dravio\work_u7"] `
      [-UnityOut "Assets\Dravio\Character\Imported"] [-Unity "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"] `
      [-Overrides "Assets\Dravio\Character\Data\asset-overrides.u7-local.json"] [-NoImages]

Steps (source GLBs are only read and hash-checked at the end; nothing in -OldWork is changed):
  1. before: every <OldWork>\assets\<Name>\<Name>_Rigged.blend (your U1-CHAR.6 run) measured again with the
     current anim_qa.py and the SAME limits -> <Work>\qa_before
  2. after: batch_import.py with rig v6 (U1-CHAR.7: water/air/perch/burrow/greet clips, transition + motion-envelope QA; 7b: Froude cadence, Strouhal speeds, leg-driven take-off, landing flare) -> <Work>, QA images unless -NoImages
  3. <Work>\U7_before_after.md (compare_animqa.py) and <Work>\reports\ (build_character_reports.py):
     qa\<Name>.md per model, prefab-catalog.md/json, animation-report.md, performance-budget.md
  4. optional, with -UnityOut and -Unity: Unity batch mode imports the manifest (prefabs, materials,
     Animator overrides, DragonVisualCatalog) and runs the EditMode tests; logs in <Work>\unity\.
     Approved assets (<UnityOut>\approved-assets.json) are never re-delivered or re-imported.
     No scene is opened or saved by this script.
Send back: U7_before_after.md, batch-report.md, reports\ (whole folder), qa_after\*.png, unity\*.xml/log.
Results are about YOUR models only after you have looked at them in Dragon Animation Lab; this script
does not accept (approve) any asset.
#>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Blender,
    [string]$OldWork = "",
    [string]$Work = "",
    [string]$UnityOut = "",
    [string]$Unity = "",
    [string]$Overrides = "",
    [string]$Python = "python",
    [switch]$NoImages,
    [switch]$DotNet   # also run the Character .NET tests (allocation, pooling, motion contract) and the CPU benchmark
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here "..\..\..")).Path
if (-not $Work) { $Work = Join-Path (Split-Path -Parent $Source) "work_u7" }
if (-not $Overrides) { $Overrides = Join-Path $repo "Assets\Dravio\Character\Data\asset-overrides.u7-local.json" }

if (-not (Test-Path $Source -PathType Container)) { throw "Source folder not found: $Source" }
$glbs = @(Get-ChildItem -Path $Source -Recurse -File | Where-Object { $_.Extension -ieq ".glb" })
if ($glbs.Count -eq 0) { throw "No .glb files in $Source (searched subfolders too)." }
if (-not (Test-Path $Blender -PathType Leaf)) { throw "Blender not found: $Blender" }
if ($Unity -and -not $UnityOut) { throw "-Unity needs -UnityOut (the import reads <UnityOut>\import-manifest.json)." }
if ($Unity -and -not (Test-Path $Unity -PathType Leaf)) { throw "Unity not found: $Unity" }
$workFull = [IO.Path]::GetFullPath($Work)
if ($OldWork -and ((Resolve-Path $OldWork).Path -eq $workFull)) { throw "-Work must differ from -OldWork (the old results are kept as 'before')." }
if ($workFull.StartsWith([IO.Path]::GetFullPath($Source))) { throw "-Work must not be inside the source folder." }
$sample = @($glbs | Where-Object { $_.Name -like "SAMPLE_*" })
if ($sample.Count -gt 0) { Write-Warning "$($sample.Count) SAMPLE_* file(s) in the source: they are reported as SAMPLE, not as real models." }
Write-Host "Found $($glbs.Count) GLB file(s) in $Source"
$expected = @("01_emberling", "02_droplet", "03_sprout", "04_zephyr", "05_steamdrake", "06_magma", "07_sparkwing")
$missing = @($expected | Where-Object { $n = $_; -not ($glbs | Where-Object { $_.BaseName -eq $n }) })
if ($missing.Count -gt 0) { Write-Warning ("Expected GLB(s) not found: " + ($missing -join ", ") + " (the run continues with what is there)") }

New-Item -ItemType Directory -Force -Path $Work | Out-Null
$before = Join-Path $Work "qa_before"; $after = Join-Path $Work "qa_after"; $reports = Join-Path $Work "reports"
New-Item -ItemType Directory -Force -Path $before, $after, $reports | Out-Null
$hashesBefore = @{}; foreach ($g in $glbs) { $hashesBefore[$g.FullName] = (Get-FileHash $g.FullName -Algorithm SHA256).Hash }

function Invoke-BlenderScript([string]$script, [string[]]$scriptArgs) {
    & $Blender --factory-startup --background --python-exit-code 1 --python (Join-Path $here $script) -- @scriptArgs
    return $LASTEXITCODE
}
$img = @(); if (-not $NoImages) { $img = @("--images") }

# 1. before
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

# 2. after
$batch = @((Join-Path $here "batch_import.py"), $Source, $Work, "--blender", $Blender, "--overrides", $Overrides)
if ($UnityOut) { $batch += @("--unity-out", $UnityOut) }
if (-not $NoImages) { $batch += "--qa-images" }
Write-Host "Batch (rig v6): $Python $($batch -join ' ')"
& $Python @batch
$batchExit = $LASTEXITCODE
Get-ChildItem -Path (Join-Path $Work "assets") -Recurse -File -Include "*_animqa.json", "*_animqa.png" -ErrorAction SilentlyContinue |
    Copy-Item -Destination $after -Force

# 3. reports
$md = Join-Path $Work "U7_before_after.md"
if ($OldWork) {
    & $Python (Join-Path $here "compare_animqa.py") $before $after $md --json (Join-Path $Work "U7_before_after.json") `
        --before-label "rig v4 (U1-CHAR.6)" --after-label "rig v6 (U1-CHAR.7b)"
}
& $Python (Join-Path $here "build_character_reports.py") $Work $reports
if ($LASTEXITCODE -ne 0) { Write-Warning "build_character_reports.py failed" }

# 3b. .NET checks (optional): runtime contract tests incl. zero per-frame allocation, and the CPU benchmark
if ($DotNet) {
    $dn = Join-Path $Work "dotnet"; New-Item -ItemType Directory -Force -Path $dn | Out-Null
    & dotnet test (Join-Path $repo "Assets\Dravio\Character\Tests\DotNet~\Dravio.Character.Tests.csproj") --logger "trx;LogFileName=character-tests.trx" --results-directory $dn
    if ($LASTEXITCODE -ne 0) { Write-Warning "Character .NET tests failed: see $dn" }
    & dotnet run -c Release --project (Join-Path $repo "Assets\Dravio\Character\Tests\DotNet~\Bench\Dravio.Character.Bench.csproj") | Tee-Object -FilePath (Join-Path $dn "bench.txt")
}

# 4. Unity (optional)
$unityExit = 0
if ($Unity) {
    $ulog = Join-Path $Work "unity"; New-Item -ItemType Directory -Force -Path $ulog | Out-Null
    Write-Host "Unity: import manifest"
    & $Unity -batchmode -nographics -projectPath $repo -executeMethod Dravio.Character.Editor.CharacterBatchCli.RunImport -logFile (Join-Path $ulog "import.log") | Out-Null
    $unityExit = $LASTEXITCODE
    if ($unityExit -ne 0) { Write-Warning "Unity import reported failures: see $ulog\import.log and $UnityOut\unity-import-report.txt" }
    Write-Host "Unity: EditMode tests"
    & $Unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testResults (Join-Path $ulog "editmode-results.xml") -logFile (Join-Path $ulog "tests.log") | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "EditMode tests failed: see $ulog\editmode-results.xml"; $unityExit = 1 }
}

foreach ($g in $glbs) {
    if ((Get-FileHash $g.FullName -Algorithm SHA256).Hash -ne $hashesBefore[$g.FullName]) { throw "Source GLB changed: $($g.FullName)" }
}
Write-Host ""
Write-Host "Source GLBs unchanged ($($glbs.Count) checked)."
if ($OldWork) { Write-Host "Before/after: $md" }
Write-Host "Reports:      $reports"
Write-Host "Batch report: $(Join-Path $Work 'batch-report.md')"
Write-Host "QA images:    $after"
Write-Host "Next: in Unity open Dravio > Character > Animation Lab: review every clip, the Behaviour preview (water / air / perch / underground intents, transitions, canTranslate) and the turn/climb sliders."
Write-Host "Then Dravio > Character > Create Stress Test Scene, set behaviourIntents on DragonStressHarness and play on the device for 5/15/30 dragons."
Write-Host "Delete old Animation\DragonBase_*.controller first so the U1-CHAR.7 states (SwimIdle..Glide, Greet..BurrowEmerge) are generated."
if ($batchExit -ne 0) { exit $batchExit }
exit $unityExit
