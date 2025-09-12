
:start
@echo off
echo [%DATE% %TIME%] >> %~dp0AutoWipeLogs.log
setlocal enableextensions enabledelayedexpansion

IF not EXIST "AutoWipeConfig.txt" (
	echo [%DATE% %TIME%]  AutoWipeconfig not Found >> %~dp0AutoWipeLogs.log
	echo Crate Text file AutoWipeConfig.txt  with your own server.cfg  and SteamCMD location
	timeout 5
	GOTO ServerStart
)
IF not EXIST ".\oxide\data\wipe.json" (
	echo [%DATE% %TIME%]  .\oxide\data\wipe.json not Found >> %~dp0AutoWipeLogs.log
	GOTO ServerStart
)
Powershell.exe -File TryParse.ps1
if not EXIST "TEMP.txt" (
	echo Error when reading oxide/data/wipe.json
	echo [%DATE% %TIME%]  Error when reading oxide/data/wipe.json >> %~dp0AutoWipeLogs.log
	timeout 5
	GOTO ServerStart
)
set Line=0
for /f "tokens=*" %%x in (AutoWipeConfig.txt) do (
	set path[!Line!]=%%x
	set /a Line+=1
)
if "!path[0]!" == "C:\example\server\my_server_identity\cfg\server.cfg" (
	echo Please Put your server.cfg path in AutoWipeConfig.txt
	echo [%DATE% %TIME%]  Please Put your server.cfg path in AutoWipeConfig.txt >> %~dp0AutoWipeLogs.log
	DiscordSendWebhook.exe  -m "Please Put your server.cfg path in AutoWipeConfig.txt" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
	timeout 5 
	del TEMP.txt
	GOTO ServerStart
)
set count=0
for /f "tokens=*" %%x in (TEMP.txt) do (
	set var[!count!]=%%x
	set /a count+=1
)
if "!var[0]!" == "False" (
if "!var[5]!" == "True" (
	echo Updating the server 
	DiscordSendWebhook.exe  -m "Updating the server" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
	GOTO Update
)
	echo [%DATE% %TIME%] No Changes... >> %~dp0AutoWipeLogs.log
	DiscordSendWebhook.exe  -m "Starting server without any changes" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
	del TEMP.txt
	GOTO ServerStart
)

set "config=!path[0]!"
if "!var[6]!" == "True" (
	GOTO CustomMap
)
if "!var[6]!" == "False" (
echo [%DATE% %TIME%] Changing server.seed & server.worldsize >> %~dp0AutoWipeLogs.log
DiscordSendWebhook.exe  -m "Changing server.seed & server.worldsize" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.levelurl" (
	>>"%config%" echo(//server.levelurl
)else (
	>>"%config%" echo(%%l
)

for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.seed" (
	>>"%config%" echo(server.seed %var[1]%
)else (
	>>"%config%" echo(%%l
)

for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.worldsize" (
	>>"%config%" echo(server.worldsize %var[2]%
)else (
	>>"%config%" echo(%%l
)

for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="//server.seed" (
	>>"%config%" echo(server.seed %var[1]%
)else (
	>>"%config%" echo(%%l
)

for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="//server.worldsize" (
	>>"%config%" echo(server.worldsize %var[2]%
)else (
	>>"%config%" echo(%%l
)

type "%config%"
)
if "!var[4]!" == "True" (
DiscordSendWebhook.exe  -m "Deleting all map and blueprints files" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
FOR /D /R %%G IN (".\server\*") DO (  rem Iterate through all subfolders
  IF EXIST %%G CD %%G
  for /f "skip=4" %%x in (%~dp0AutoWipeConfig.txt) do (
	set FileName=%%x
	IF EXIST !FileName! (
	DEL !FileName!
	echo [%DATE% %TIME%] deleting !FileName! >> %~dp0AutoWipeLogs.log
	)
)
:CustomMap
if "!var[6]!" == "True" (
DiscordSendWebhook.exe  -m "Changing server.levelurl" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
echo [%DATE% %TIME%] Changing server.levelurl >> %~dp0AutoWipeLogs.log
for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.levelurl" (
	>>"%config%" echo(server.levelurl %var[7]%
)else (
	>>"%config%" echo(%%l
)
for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="//server.levelurl" (
	>>"%config%" echo(server.levelurl %var[7]%
)else (
	>>"%config%" echo(%%l
)
for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.seed" (
	>>"%config%" echo(//server.seed %var[1]%
)else (
	>>"%config%" echo(%%l
)

for /f "tokens=*" %%l in ('type "%config%"^&cd.^>"%config%"'
) do for /f "tokens=1 delims== " %%a in ("%%~l"
) do if /i "%%~a"=="server.worldsize" (
	>>"%config%" echo(//server.worldsize %var[2]%
)else (
	>>"%config%" echo(%%l
)
)
)
)
:Update
if "!var[5]!" == "True" (
	IF not EXIST "!path[1]!" (
	echo Please Put your server.cfg and SteamCMD path in AutoWipeConfig.txt
	echo [%DATE% %TIME%] Please Put  your server.cfg and SteamCMD in AutoWipeConfig.txt >> %~dp0AutoWipeLogs.log
	DiscordSendWebhook.exe  -m "Please Put  your server.cfg and SteamCMD path in AutoWipeConfig.txt" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
	timeout 5
	GOTO ServerStart
)
	echo [%DATE% %TIME%] Updating the server >> %~dp0AutoWipeLogs.log
	DiscordSendWebhook.exe  -m "Updating the server" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
	if not EXIST "!path[2]!" (
		echo Please change unzip.exe path to your unzip.exe location
		echo [%DATE% %TIME%] Please change unzip.exe path to your unzip.exe location >> %~dp0AutoWipeLogs.log
		DiscordSendWebhook.exe  -m "Please change unzip.exe path to your unzip.exe location" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w %path[3]%
		timeout 5
		GOTO ServerStart
	)
	!path[1]! +login anonymous +force_install_dir %~dp0 +app_update 258550  +quit
	curl -SL -A "Mozilla/5.0" "https://umod.org/games/rust/download" --output %~dp0oxidemod.zip
	if exist "%~dp0oxidemod.zip" ( echo !%~dp0!)
	"!path[2]!" -o %~dp0oxidemod.zip -d %~dp0
	rem if the first method didn't work use this one 
	
	rem curl -SL -A "Mozilla/5.0" "https://umod.org/games/rust/download" --output %~dp0oxidemod.zip
	rem tar -xvf %~dp0oxidemod.zip
	del %~dp0oxidemod.zip
)
endlocal
del TEMP.txt
:ServerStart
echo [%DATE% %TIME%] Starting the server >> %~dp0AutoWipeLogs.log
DiscordSendWebhook.exe  -m "Starting the server" -a https://cdn-icons-png.flaticon.com/512/1999/1999208.png -w "!path[3]!"
@echo on
RustDedicated.exe -batchmode +rcon.port 28570 +rcon.web 1 +rcon.password 123
@echo off 
[%DATE% %TIME%] Restarting >> %~dp0AutoWipeLogs.log
GOTO start