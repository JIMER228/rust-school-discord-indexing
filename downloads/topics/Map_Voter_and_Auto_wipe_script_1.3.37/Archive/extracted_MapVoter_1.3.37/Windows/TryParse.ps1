if ((Test-Path .\oxide\data\wipe.json)) {
$wipeConfig = Get-Content .\oxide\data\wipe.json | ConvertFrom-Json
Add-Content -Path TEMP.txt -Value $wipeConfig.isWipeDay
Add-Content -Path TEMP.txt -Value $wipeConfig.MapSeed
Add-Content -Path TEMP.txt -Value $wipeConfig.MapSize
Add-Content -Path TEMP.txt -Value $wipeConfig.WipeDate
Add-Content -Path TEMP.txt -Value $wipeConfig.FullWipe
Add-Content -Path TEMP.txt -Value $wipeConfig.ForcedWipe
Add-Content -Path TEMP.txt -Value $wipeConfig.iSCustomMap
Add-Content -Path TEMP.txt -Value $wipeConfig.LevelUrl
}
