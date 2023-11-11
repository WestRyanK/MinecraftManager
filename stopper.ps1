$Folder = "C:/Program Files/minecraft_servers/lee_mindcrap_server"

$Process = [System.Diagnostics.Process]::new()
$Process.StartInfo.FileName = "java"
$Process.StartInfo.Arguments = "-jar server.jar --nogui"
$Process.StartInfo.WorkingDirectory = $Folder
$Process.StartInfo.RedirectStandardInput = $true
$Process.StartInfo.UseShellExecute = $false
$Process.Start()

Write-Host "Waiting 20 seconds"
Start-Sleep -Seconds 20
Write-Host "Writing stop"
$Process.StandardInput.WriteLine("stop")

Write-Host "Waiting for server to stop"
$Process.WaitForExit()
Write-Host "Server stopped"