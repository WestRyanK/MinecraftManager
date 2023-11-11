param(
    [string] $LogPath = $null,
    [string] $ServerPath = $null
    )

$Folder = "C:\Program Files\minecraft_servers\lee_mindcrap_server"
if (!$ServerPath) {
    $ServerPath = "$Folder/lee_mindcrap_server.bat"
}
if (!$LogPath) {
    $LogPath = "$Folder/logs/latest.log"
}

$TimeRegex = "\[(?<Time>\d+:\d+:\d+)\]"
$LevelRegex = "\[Server thread/(?<Level>INFO)\]:"
$PlayerRegex = "<(?<Player>.+)>"
$CommandRegex = "(?<Command>.+)"
$Regex = "^$TimeRegex $LevelRegex $PlayerRegex $CommandRegex$"

Get-Content -Path $LogPath -Wait -Last 1 | Foreach-Object {
    Write-Host $_
    if ($_ -match $Regex) {
        $Time = $Matches.Time
        $Level = $Matches.Level
        $Player = $Matches.Player
        $Command = $Matches.Command
        Write-Host "($Time) ($Level) ($Player) ($Command)"
        if ($Command -eq "Shutdown") {
            Write-Host "***Stopping Minecraft***"
        }
    }
}
