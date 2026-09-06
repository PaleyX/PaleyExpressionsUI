# Downloads Ace build and places it in Asp ireApp1.Web/wwwroot/lib/ace
# Run from repository root (PowerShell):
#   .\scripts\download-ace.ps1

param(
	[string]$SolutionRoot = "$(Resolve-Path .).Path"
)

$webRoot = Join-Path $SolutionRoot "AspireApp1.Web\wwwroot"
$dest = Join-Path $webRoot "lib\ace"

Write-Host "Solution root: $SolutionRoot"
Write-Host "Target Ace folder: $dest"

if (-Not (Test-Path $webRoot)) {
	Write-Error "Could not find wwwroot at $webRoot"
	exit 1
}

# Ensure destination exists
if (-Not (Test-Path $dest)) {
	New-Item -ItemType Directory -Path $dest -Force | Out-Null
}

$zip = Join-Path $env:TEMP "ace_build.zip"
$extract = Join-Path $env:TEMP "ace_build_extract"

if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }

$uri = 'https://github.com/ajaxorg/ace-builds/archive/refs/heads/master.zip'
Write-Host "Downloading Ace from $uri ..."

try {
	Invoke-WebRequest -Uri $uri -OutFile $zip -UseBasicParsing -ErrorAction Stop
} catch {
	Write-Error "Download failed: $_"
	exit 1
}

Write-Host "Extracting..."
try {
	Expand-Archive -Path $zip -DestinationPath $extract -Force
} catch {
	Write-Error "Extract failed: $_"
	exit 1
}

$source = Join-Path $extract "ace-builds-master\src-min-noconflict"
if (-Not (Test-Path $source)) {
	Write-Error "Expected source folder not found: $source"
	exit 1
}

Write-Host "Copying files to $dest ..."
try {
	Copy-Item -Path (Join-Path $source '*') -Destination $dest -Recurse -Force
} catch {
	Write-Error "Copy failed: $_"
	exit 1
}

Write-Host "Cleaning up..."
Remove-Item $zip -Force
Remove-Item $extract -Recurse -Force

Write-Host "Done. Ace files are in $dest"
Write-Host "Ensure Asp ireApp1.Web/Components/App.razor references /lib/ace/ace.js and restart the app."
