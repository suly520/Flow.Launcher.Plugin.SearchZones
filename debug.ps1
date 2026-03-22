param (
    [string]$Plugin = "SearchZones"
)

$AppDataFolder = [Environment]::GetFolderPath("ApplicationData")
$flowLauncherExe = "$env:LOCALAPPDATA\FlowLauncher\Flow.Launcher.exe"

if (-not (Test-Path $flowLauncherExe)) {
    Write-Host "Flow.Launcher.exe not found. Please install Flow Launcher first"
    exit 1
}

Stop-Process -Name "Flow.Launcher" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

function Deploy-Plugin {
    param([string]$ProjectName, [string]$FolderName)

    Write-Host "Publishing $ProjectName..."
    dotnet publish $ProjectName -c Debug -r win-x64 --no-self-contained

    $dest = "$AppDataFolder\FlowLauncher\Plugins\$FolderName"
    if (Test-Path $dest) {
        Remove-Item -Recurse -Force $dest
    }

    Copy-Item "$ProjectName\bin\Debug\win-x64\publish" "$AppDataFolder\FlowLauncher\Plugins\" -Recurse -Force
    Rename-Item -Path "$AppDataFolder\FlowLauncher\Plugins\publish" -NewName $FolderName
    Write-Host "Deployed $FolderName"
}

if ($Plugin -eq "all" -or $Plugin -eq "SearchZones") {
    Deploy-Plugin "Flow.Launcher.Plugin.SearchZones" "SearchZones"
}

Start-Sleep -Seconds 2
Start-Process $flowLauncherExe
