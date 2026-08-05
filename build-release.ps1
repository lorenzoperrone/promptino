# build-release.ps1
# Automates the release packaging for Promptino's open-source distribution on GitHub.
# Requires Inno Setup 7+ for installer compilation (optional step).

# Ensure we are in the project root
if (-not (Test-Path "Promptino.slnx") -and -not (Test-Path "Promptino.App")) {
    Write-Error "This script must be run from the root of the Promptino project."
    exit 1
}

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Starting Promptino Open-Source Release Packaging" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Clean and setup release directory
$releaseDir = Join-Path $PSScriptRoot "release"
if (Test-Path $releaseDir) {
    Write-Host "Cleaning existing release directory..." -ForegroundColor Yellow
    Remove-Item -Path $releaseDir -Recurse -Force
}
Write-Host "Creating fresh release directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

# 2. Copy source code and files for GitHub distribution
Write-Host "Cloning project files to release/..." -ForegroundColor Yellow
$foldersToCopy = @(".github", "Promptino.App", "Promptino.App.Tests", "Promptino.Core", "Promptino.Platform", "Promptino.Storage")
foreach ($folder in $foldersToCopy) {
    if (Test-Path $folder) {
        Copy-Item -Path $folder -Destination (Join-Path $releaseDir $folder) -Recurse -Force
    }
}

$filesToCopy = @("Promptino.slnx", ".gitignore", "README.md", "LICENSE", "NOTICE.md", "ARCHITECTURE.md")
foreach ($file in $filesToCopy) {
    if (Test-Path $file) {
        Copy-Item -Path $file -Destination $releaseDir -Force
    }
}

# Clean build folders in the release copy
Write-Host "Cleaning bin/ and obj/ folders from the release copy..." -ForegroundColor Yellow
Get-ChildItem -Path $releaseDir -Directory -Recurse -Filter "bin" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $releaseDir -Directory -Recurse -Filter "obj" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 3. Compile and publish the release binaries
Write-Host "Publishing self-contained win-x64 app..." -ForegroundColor Yellow
# Run clean on the current dev environment before publish to be safe
dotnet clean Promptino.App/Promptino.App.csproj -c Release | Out-Null
dotnet publish Promptino.App/Promptino.App.csproj -p:PublishProfile=win-x64-release

# 4. Verify publish directory exists
$publishDir = Join-Path $PSScriptRoot "Promptino.App/bin/Release/net10.0/publish/win-x64"
if (-not (Test-Path $publishDir)) {
    Write-Error "Publish directory not found at $publishDir"
    exit 1
}

# 5. Copy binaries to a subfolder within release/
$binariesDir = Join-Path $releaseDir "binaries"
Write-Host "Creating binaries subfolder in release/binaries/..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $binariesDir | Out-Null

Write-Host "Copying compiled binaries to release/binaries/..." -ForegroundColor Yellow
Copy-Item -Path "$publishDir\*" -Destination $binariesDir -Recurse -Force
Get-ChildItem -Path $binariesDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

# 6. Compile Inno Setup installer (optional — requires ISCC)
Write-Host "Checking for Inno Setup Compiler (ISCC)..." -ForegroundColor Yellow
$isccPath = $null

# Try PATH first
$isccPath = (Get-Command "iscc" -ErrorAction SilentlyContinue).Source

# Try common Inno Setup 7 installation paths
if (-not $isccPath) {
    $commonPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup\ISCC.exe"
    )
    foreach ($p in $commonPaths) {
        if (Test-Path $p) {
            $isccPath = $p
            break
        }
    }
}

# Fallback to env var
if (-not $isccPath -and $env:ISCC_PATH) {
    if (Test-Path $env:ISCC_PATH) {
        $isccPath = $env:ISCC_PATH
    }
}

if ($isccPath) {
    Write-Host "Found ISCC at: $isccPath" -ForegroundColor Green

    # Copy Promptino.iss to release/ for a self-contained release package
    Copy-Item -Path (Join-Path $PSScriptRoot "Promptino.iss") -Destination $releaseDir -Force

    # Pass version via ISCC /d define if PROMPTINO_VERSION is set
    $isccArgs = @( (Join-Path $PSScriptRoot "Promptino.iss") )
    if ($env:PROMPTINO_VERSION) {
        $isccArgs = @("/dMyAppVersion=$($env:PROMPTINO_VERSION)") + $isccArgs
    }

    Write-Host "Compiling installer..." -ForegroundColor Yellow
    & $isccPath $isccArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer compiled successfully!" -ForegroundColor Green
    } else {
        Write-Host "Installer compilation failed (exit code: $LASTEXITCODE)." -ForegroundColor Red
        Write-Host "Check the ISCC output above for details."
    }
} else {
    Write-Host "Inno Setup Compiler (ISCC) not found - skipping installer build." -ForegroundColor Yellow
    Write-Host "To compile the installer, install Inno Setup 7 from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Or set ISCC_PATH environment variable to ISCC.exe location." -ForegroundColor Yellow
}

Write-Host "=========================================" -ForegroundColor Green
Write-Host " Release Packaging Completed Successfully!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Output Directory: $releaseDir" -ForegroundColor White
Write-Host "  - Project Source Code: copied and cleaned" -ForegroundColor White
Write-Host "  - Compiled Binaries:   copied to $binariesDir" -ForegroundColor White
if ($isccPath) {
    Write-Host "  - Installer:            compiled to release/installer" -ForegroundColor White
}
