# Generate manifest.json for incremental updates
param(
    [string]$Version = "1.1.7",
    [string]$ReleaseDir = "bin/Release/net8.0-windows",
    [string]$BaseUrl = "https://github.com/ghassanelgendy/chronos-screentime/releases/download/v1.1.7"
)

Write-Host "Generating manifest for version $Version..." -ForegroundColor Green

# Check if release directory exists
if (-not (Test-Path $ReleaseDir)) {
    Write-Host "Error: Release directory '$ReleaseDir' not found!" -ForegroundColor Red
    Write-Host "Please build your project first." -ForegroundColor Yellow
    exit 1
}

$manifestPath = Join-Path $ReleaseDir "manifest.json"

Write-Host "Scanning files in $ReleaseDir..." -ForegroundColor Yellow

# Get all files recursively
$files = Get-ChildItem -Path $ReleaseDir -Recurse -File

Write-Host "Found $($files.Count) files. Computing hashes..." -ForegroundColor Yellow

$manifest = @{
    version = $Version
    base_url = $BaseUrl
    files = @()
}

$processedCount = 0
foreach ($file in $files) {
    $processedCount++
    Write-Progress -Activity "Computing file hashes" -Status "Processing $($file.Name)" -PercentComplete (($processedCount / $files.Count) * 100)
    
    # Skip the manifest file itself if it already exists
    if ($file.Name -eq "manifest.json") {
        continue
    }
    
    try {
        $hash = Get-FileHash $file.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash
        $relPath = $file.FullName.Substring((Resolve-Path $ReleaseDir).Path.Length + 1).Replace("\", "/")
        
        $manifest.files += @{
            path = $relPath
            hash = $hash.ToLower()
        }
        
        Write-Host "  OK: $relPath" -ForegroundColor Green
    }
    catch {
        Write-Host "  ERROR: $($file.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Progress -Activity "Computing file hashes" -Completed

# Convert to JSON and save
try {
    $json = $manifest | ConvertTo-Json -Depth 5
    $json | Set-Content $manifestPath -Encoding UTF8
    
    Write-Host ""
    Write-Host "Manifest generated successfully!" -ForegroundColor Green
    Write-Host "Location: $manifestPath" -ForegroundColor Cyan
    Write-Host "Files included: $($manifest.files.Count)" -ForegroundColor Cyan
    Write-Host "Version: $Version" -ForegroundColor Cyan
    Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Upload all files in '$ReleaseDir' to your GitHub release" -ForegroundColor White
    Write-Host "2. Make sure 'manifest.json' is included in the release assets" -ForegroundColor White
    Write-Host "3. Update your app to use the manifest URL: $BaseUrl/manifest.json" -ForegroundColor White
}
catch {
    Write-Host "Error saving manifest: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 