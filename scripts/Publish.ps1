# Publica, atualiza a instalação do usuário, configura startup e reinicia o app.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$publishedApp = Join-Path $projectRoot 'artifacts\app'
$installedApp = Join-Path $env:LOCALAPPDATA 'WlyUsage\app'
$installedExe = Join-Path $installedApp 'WlyUsage.exe'
& dotnet publish (Join-Path $projectRoot 'WlyUsage.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $projectRoot 'artifacts\package') --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'A publicação falhou; a instalação anterior foi preservada.' }
foreach ($usageProcess in (Get-Process WlyUsage -ErrorAction SilentlyContinue)) {
    if ($usageProcess.Path -and ($usageProcess.Path.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $usageProcess.Path -eq $installedExe)) {
        Stop-Process -Id $usageProcess.Id
    }
}
New-Item -ItemType Directory -Path $installedApp, $publishedApp -Force | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'artifacts\package\*') -Destination $installedApp -Recurse -Force
Copy-Item -Path (Join-Path $projectRoot 'artifacts\package\*') -Destination $publishedApp -Recurse -Force
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcutPaths = @((Join-Path $projectRoot 'Abrir WLY Usage.lnk'), (Join-Path ([Environment]::GetFolderPath('Startup')) 'WLY Usage.lnk'))
foreach ($shortcutPath in $shortcutPaths) {
    $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installedApp
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = 'WLY Usage - monitor local de Claude e Codex'
    $shortcut.Save()
}
$startedUsage = Start-Process -FilePath $installedExe -WorkingDirectory $installedApp -WindowStyle Hidden -PassThru
$startedUsage.Id | Set-Content (Join-Path $projectRoot 'artifacts\running.pid')
Start-Sleep -Seconds 2
Get-Process -Id $startedUsage.Id | Select-Object Id, Responding, Path
