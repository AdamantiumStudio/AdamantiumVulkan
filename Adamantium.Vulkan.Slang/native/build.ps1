<#
.SYNOPSIS
  Builds the slang-c-shared shim on Windows and stages it (+ the Slang runtime) next to the C# project.
.NOTES
  Slang is taken from $env:SLANG_DIR, else $env:VULKAN_SDK (the Vulkan SDK bundles Slang).
  Usage:  pwsh native\build.ps1 [-Config Release|Debug]
#>
[CmdletBinding()]
param([string]$Config = "Release")
$ErrorActionPreference = "Stop"

$native  = $PSScriptRoot
$project = Split-Path $native -Parent

$slang = if ($env:SLANG_DIR) { $env:SLANG_DIR } elseif ($env:VULKAN_SDK) { $env:VULKAN_SDK } else { $null }
if (-not $slang) { throw "Set SLANG_DIR or VULKAN_SDK to a Slang / Vulkan SDK directory." }

# cmake: from PATH, else the copy bundled with Visual Studio.
$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (-not $cmake) {
    $cmake = (Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -ErrorAction SilentlyContinue |
              Select-Object -First 1).FullName
}
if (-not $cmake) { throw "cmake not found. Install it, or the 'C++ CMake tools' workload in Visual Studio." }

& $cmake -S $native -B "$native\build" -A x64 -DSLANG_DIR="$slang"
if ($LASTEXITCODE) { throw "cmake configure failed" }
& $cmake --build "$native\build" --config $Config
if ($LASTEXITCODE) { throw "cmake build failed" }

# Stage the shim + Slang runtime + its language server under runtimes\win-x64\native, which the C# project packs as it
# is. All from one Slang build: slangd loads the same slang-compiler.dll the bindings do. The 2026.17 SDK no longer
# ships slangd.dll - slangd.exe imports slang-compiler.dll itself.
$stage = Join-Path $project "runtimes\win-x64\native"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$native\build\$Config\slang-c-shared.dll" $stage -Force
$runtime = "slang.dll", "slang-compiler.dll", "slang-glslang.dll", "slangd.exe"
foreach ($file in $runtime) {
    Copy-Item "$slang\Bin\$file" $stage -Force
}
Write-Host "OK: slang-c-shared.dll + $($runtime -join ', ') -> $stage" -ForegroundColor Green
