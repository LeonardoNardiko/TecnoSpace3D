$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$srcDir = Join-Path $root "src"
$binDir = Join-Path $root "bin"

if (!(Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir | Out-Null
}

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    throw ".NET Framework 4.x x64 compiler (csc.exe) not found at $csc"
}

$outExe = Join-Path $binDir "TecnoSpace3D.exe"
$csFiles = Get-ChildItem $srcDir -Filter "*.cs" | ForEach-Object { $_.FullName }

$defines = @()
$linkArgs = @()

# 1. Auto-detect SOLIDWORKS Interop
$swDir = Join-Path $env:ProgramFiles "SOLIDWORKS Corp\SOLIDWORKS"
$swDll1 = Join-Path $swDir "SolidWorks.Interop.sldworks.dll"
$swDll2 = Join-Path $swDir "SolidWorks.Interop.swconst.dll"
if ((Test-Path $swDll1) -and (Test-Path $swDll2)) {
    $defines += "HAS_SW"
    $linkArgs += "/link:$swDll1"
    $linkArgs += "/link:$swDll2"
    Write-Output "[+] SOLIDWORKS SDK detected"
}

# 2. Auto-detect Autodesk Inventor Interop (any installed year)
$invDll = Get-ChildItem (Join-Path $env:ProgramFiles "Autodesk") -Filter "Autodesk.Inventor.Interop.dll" -Recurse -ErrorAction SilentlyContinue |
          Sort-Object FullName -Descending | Select-Object -First 1
if ($invDll) {
    $defines += "HAS_INVENTOR"
    $linkArgs += "/link:$($invDll.FullName)"
    Write-Output "[+] Autodesk Inventor SDK detected"
}

# 3. Auto-detect Siemens Solid Edge Interop (any installed version)
$seDll = Get-ChildItem (Join-Path $env:ProgramFiles "Siemens") -Filter "interop.SolidEdgeFrameworkLib.dll" -Recurse -ErrorAction SilentlyContinue |
         Sort-Object FullName -Descending | Select-Object -First 1
if ($seDll) {
    $defines += "HAS_SOLIDEDGE"
    $linkArgs += "/link:$($seDll.FullName)"
    Write-Output "[+] Siemens Solid Edge SDK detected"
}

# 4. Auto-detect Autodesk AutoCAD Interop (any installed year)
$acDir = Get-ChildItem (Join-Path $env:ProgramFiles "Autodesk") -Filter "AutoCAD*" -ErrorAction SilentlyContinue |
         Where-Object { $_.PSIsContainer -and (Test-Path (Join-Path $_.FullName "Autodesk.AutoCAD.Interop.dll")) } |
         Sort-Object Name -Descending | Select-Object -First 1
if ($acDir) {
    $acDll1 = Join-Path $acDir.FullName "Autodesk.AutoCAD.Interop.dll"
    $acDll2 = Join-Path $acDir.FullName "Autodesk.AutoCAD.Interop.Common.dll"
    if ((Test-Path $acDll1) -and (Test-Path $acDll2)) {
        $defines += "HAS_AUTOCAD"
        $linkArgs += "/link:$acDll1"
        $linkArgs += "/link:$acDll2"
        Write-Output "[+] Autodesk AutoCAD SDK detected"
    }
}

$defineArg = if ($defines.Count -gt 0) { "/define:" + ($defines -join ";") } else { $null }

$argsList = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/debug-",
    "/out:$outExe",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Web.Extensions.dll",
    "/r:Microsoft.CSharp.dll"
)
if ($defineArg) { $argsList += $defineArg }
$argsList += $linkArgs
$argsList += $csFiles

Write-Output "Building TecnoSpace3D.exe (x64 Standalone Multi-CAD)..."
& $csc $argsList
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (exit code $LASTEXITCODE)"
}

$sizeKb = [Math]::Round((Get-Item $outExe).Length / 1KB, 1)
Write-Output "BUILD SUCCESS! Output: $outExe ($sizeKb KB)"
