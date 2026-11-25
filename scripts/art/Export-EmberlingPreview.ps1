param(
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe",
    [ValidateSet("Wander","Care","Polished")][string]$Mode = "Care"
)
$ErrorActionPreference = "Stop"
Push-Location (Join-Path $PSScriptRoot "../..")
try {
    if (!(Test-Path "ProjectSettings/ProjectVersion.txt")) { throw "Not a Unity project root." }
    if (!(Test-Path $Blender -PathType Leaf)) { throw "Blender executable not found: $Blender" }
    $name = "Emberling$Mode"
    $flag = switch ($Mode) { "Polished" { "--polish-preview" } "Care" { "--care-preview" } "Wander" { "--walk-preview" } }
    $export = "art-exports/$name.fbx"
    & $Blender --factory-startup --background --python-exit-code 1 --python scripts/art/build_emberling_living.py -- $export $flag
    if ($LASTEXITCODE -ne 0) { throw "Blender export failed ($LASTEXITCODE)." }
    if (!(Test-Path $export -PathType Leaf) -or (Get-Item $export).Length -le 0) { throw "FBX output missing or empty." }
    $models = "Assets/Dravio/Art/Models"
    New-Item -ItemType Directory -Path $models -Force | Out-Null
    $destination = Join-Path $models "$name.fbx"
    if (Test-Path $destination) {
        Write-Warning "FBX already exists in Assets. Original preserved; review the new file in art-exports before manually replacing it."
        return
    }
    Copy-Item $export $destination
    Write-Host "Ready: $destination"
    Write-Host "In Unity, select the FBX and use the matching Dravio preview menu."
}
finally { Pop-Location }
