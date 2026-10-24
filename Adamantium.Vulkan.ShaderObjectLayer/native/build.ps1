# Builds VK_LAYER_KHRONOS_shader_object from Vulkan-ExtensionLayer on Windows and stages the DLL and its manifest under
# layers/win-x64/ next to the C# project, which packs that folder as it is.
#   Usage:  .\native\build.ps1 [-Tag vulkan-sdk-1.4.363.0]
# The tag defaults to the Vulkan SDK release the rest of this repository is generated from.
param([string]$Tag = "vulkan-sdk-1.4.363.0")

$ErrorActionPreference = "Stop"
$native = $PSScriptRoot
$project = Split-Path $native -Parent
# Out of the repository: the dependencies build several folders deep, and under a checkout's path that passes the
# 260 characters MSBuild's file tracker can write to.
$work = Join-Path ([IO.Path]::GetTempPath()) "vkel-$Tag"
$source = Join-Path $work "src"

if (-not (Test-Path $source)) {
    git clone --depth 1 --branch $Tag https://github.com/KhronosGroup/Vulkan-ExtensionLayer.git $source
    if ($LASTEXITCODE -ne 0) { throw "git clone failed" }
}

$out = Join-Path $work "out"
cmake -S $source -B $out -A x64 -D UPDATE_DEPS=ON -D CMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }
cmake --build $out --config Release --target VkLayer_khronos_shader_object
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

$layers = Join-Path $out "layers"
$library = Get-ChildItem $layers -Recurse -Filter "VkLayer_khronos_shader_object.dll" | Select-Object -First 1
$manifest = Get-ChildItem $layers -Recurse -Filter "VkLayer_khronos_shader_object.json" |
    Where-Object { $_.DirectoryName -eq $library.DirectoryName } | Select-Object -First 1
if (-not $library -or -not $manifest) { throw "The layer was not built." }

$stage = Join-Path $project "layers\win-x64"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item $library.FullName, $manifest.FullName $stage
Write-Host "OK: $($library.Name) + manifest ($Tag) -> $stage"
