# Example script to download audio from a DASH MPD manifest

# Path to executable
$executable = "dotnet run --project .\MPD-downloader\MPD-downloader.csproj"

# Sample BBC Radio MPD URL (this is just an example and may not be valid)
$mpdUrl = "https://example-dash-url.com/manifest.mpd"

# Set output filename
$outputFile = "downloaded-audio.mp4"

# Run the downloader
Write-Host "Starting MPD Downloader..." -ForegroundColor Green
Invoke-Expression "$executable --url $mpdUrl --output $outputFile"

# Check if download was successful
if ($LASTEXITCODE -eq 0) {
    Write-Host "Download completed successfully!" -ForegroundColor Green
    Write-Host "File saved to: $((Get-Item $outputFile).FullName)"
} else {
    Write-Host "Download failed with exit code: $LASTEXITCODE" -ForegroundColor Red
}