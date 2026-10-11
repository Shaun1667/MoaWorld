# Rigs every Varco3D moa model with rig_moa.py and writes <Folder>_Rigged.fbx next to each model.
# Usage: powershell -File rig_all.ps1 [-Blender D:\blender.exe] [-Only Rabbit,Tiger] [-PreviewDir <dir>] [-OutDir <dir>]
param(
    [string]$Blender = "D:\blender.exe",
    [string[]]$Only = @(),
    [string]$PreviewDir = "",
    [string]$OutDir = ""
)

$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$models = Join-Path $project "Assets\Varco3D"
$script = Join-Path $PSScriptRoot "rig_moa.py"

# Model folder -> body type
$bodyTypes = [ordered]@{
    Rabbit = "biped"; Leopardcat = "biped"; Badger = "biped"; Chipmunk = "biped"; Redfox = "biped"
    Otter = "biped"; Hedgehog = "biped"; Bear = "biped"; Dambi = "upright"
    Tiger = "quadruped"; Noru = "quadruped"; Gorani = "quadruped"; Wildboar = "quadruped"; Frog = "hopper"
    Mae = "bird"; Owl = "bird"; Ggachi = "bird"; Durumi = "bird"
    Bumblebee = "insect"; Horangnabi = "insect"; Gurungi = "snake"
}

foreach ($folder in $bodyTypes.Keys) {
    if ($Only.Count -gt 0 -and $Only -notcontains $folder) { continue }
    $obj = Join-Path $models "$folder\$folder.obj"
    $fbx = if ($OutDir) { Join-Path $OutDir "$folder`_Rigged.fbx" } else { Join-Path $models "$folder\$folder`_Rigged.fbx" }
    $args = @("--background", "--factory-startup", "--python", $script, "--", $obj, $fbx, $bodyTypes[$folder])
    if ($PreviewDir) { $args += (Join-Path $PreviewDir $folder) }
    $log = & $Blender @args 2>&1
    $summary = $log | Select-String "\[rig\] (biped|upright|quadruped|hopper|bird|insect|snake)|Traceback|Error:" | Select-Object -First 3
    Write-Output "$folder ($($bodyTypes[$folder])): $($summary -join ' | ')"
}
