# Builds the Unity MCP server (CoderGamester/mcp-unity) next to the project so Claude Code can drive the Unity Editor.
# Run once from the repo root after the Unity Editor has resolved packages (Library/PackageCache exists):
#   powershell -ExecutionPolicy Bypass -File tools/setup-unity-mcp.ps1
# Then open the project in Unity Editor (GUI, not batchmode) and start Claude Code in this folder: .mcp.json registers the server.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pkg = Get-ChildItem (Join-Path $root 'Library\PackageCache') -Directory -Filter 'com.gamelovers.mcp-unity@*' | Select-Object -First 1
if (-not $pkg) { throw 'Package not in Library/PackageCache. Open the project in Unity once so it resolves Packages/manifest.json.' }
$dest = Join-Path $root 'tools\mcp-unity-server'
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item (Join-Path $pkg.FullName 'Server~') $dest -Recurse
Push-Location $dest
try { npm install; npm run build } finally { Pop-Location }
Write-Host "Built $dest\build\index.js"
