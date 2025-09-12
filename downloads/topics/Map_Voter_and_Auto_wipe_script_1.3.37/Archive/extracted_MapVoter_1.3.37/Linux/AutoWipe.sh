#!/bin/bash
while true ; do
  echo $(date '+%d/%m/%Y %H:%M:%S')  >> $PWD/AutoWipe.log
  ServerPath="$PWD"
  if [ -e AutoWipeConfig.txt ]
  then
  	  wipeJson="oxide/data/wipe.json";
	  if [ -e  $wipeJson ]
	  then
		isWipeDay=$(cat oxide/data/wipe.json  | jq -r '.isWipeDay')
		MapSeed=$(cat oxide/data/wipe.json  | jq -r '.MapSeed')
		MapSize=$(cat oxide/data/wipe.json  | jq -r '.MapSize')
		#date
		FullWipe=$(cat oxide/data/wipe.json  | jq -r '.FullWipe')
		ForcedWipe=$(cat oxide/data/wipe.json  | jq -r '.ForcedWipe')
		iSCustomMap=$(cat oxide/data/wipe.json  | jq -r '.iSCustomMap')
		LevelUrl=$(cat oxide/data/wipe.json  | jq -r '.LevelUrl')
		arr=()
		while IFS= read -r line || [[ "$line" ]]; do
		arr+=("$line")
		done < AutoWipeConfig.txt
		if [ -e ${arr[0]} ] && [ -e ${arr[1]} ]
  		then
  			if [ "$isWipeDay" == "false" ] && [ "$ForcedWipe" == "true" ]; 
  			then
				echo $(date '+%d/%m/%Y %H:%M:%S') "Updating the server"   >> $PWD/AutoWipe.log
				./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Updating the server"
				${arr[1]} +login anonymous +force_install_dir $ServerPath +app_update 258550  +quit
				wget https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust-linux.zip -O $ServerPath/Oxide.Rust-linux.zip
				unzip -o Oxide.Rust-linux.zip
				rm Oxide.Rust-linux.zip
			fi
			if [ "$isWipeDay" == "false" ];
			then
				echo $(date '+%d/%m/%Y %H:%M:%S') "Starting server without changes"   >> $PWD/AutoWipe.log
				./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Starting server without any changes"
			fi
			if [ "$isWipeDay" == "true" ];
			then
				arraylength=${#arr[@]}
				if [ ${arraylength} > 5 ]
				then
					for (( i=6; i<${arraylength}; i++ ));
					do
						rm ${arr[$i]};
						echo $(date '+%d/%m/%Y %H:%M:%S') "Deleting ${arr[$i]}"   >> $PWD/AutoWipe.log
					done	
				fi
				if [ "$iSCustomMap" == "false" ];
				then
					echo $(date '+%d/%m/%Y %H:%M:%S') "Changing server.seed ans server.workdsize"   >> $PWD/AutoWipe.log
					./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Changing server.seed to ${MapSeed} and server.workdsize to ${MapSize}"
					sed -i "s/.*server.seed.*/server.seed $MapSeed/" "${arr[0]}"
					sed -i "s/.*server.worldsize.*/server.worldsize $MapSize/" "${arr[0]}"
					sed -i --expression  "s@.*server.levelurl.*@//server.levelurl @" "${arr[0]}"
				fi
				if [ "$iSCustomMap" == "true" ];
				then
					seed=""
					size="//server.worldsize"
					./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Changing server.levelurl to ${LevelUrl}"
					sed -i --expression  "s@.*server.seed.*@//server.seed@" "${arr[0]}"
					sed -i --expression  "s@.*server.worldsize.*@//server.worldsize@" "${arr[0]}"
					#sed -i "s/.*server.levelurl.*/server.levelurl $LevelUrl/" "${arr[0]}"
					sed -i --expression  "s@.*server.levelurl.*@server.levelurl $LevelUrl@" "${arr[0]}"
				fi
				if [ "$FullWipe" == "true" ];
				then
					# get length of an array
					arraylength=${#arr[@]}

					# use for loop to read all values and indexes
					for (( i=3; i<${arraylength}; i++ ));
					do
						IFS=$'\n'; set -f
						for f in $(find server/ -name "${arr[$i]}"); 
						do 
						rm "$f";
						echo $(date '+%d/%m/%Y %H:%M:%S') "Deleting ${arr[$i]}"   >> $PWD/AutoWipe.log
						done
						unset IFS; set +f
					done
					./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Deleting all map and blueprints files"
				fi
				if [ "$ForcedWipe" == "true" ]; 
	  			then
					echo $(date '+%d/%m/%Y %H:%M:%S') "Updating the server"   >> $PWD/AutoWipe.log
					./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Updating the server"
					${arr[1]} +login anonymous +force_install_dir $ServerPath +app_update 258550  +quit
					wget https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust-linux.zip -O $ServerPath/Oxide.Rust-linux.zip
					unzip -o Oxide.Rust-linux.zip
					rm Oxide.Rust-linux.zip
				fi
			fi
  		else
  			echo $(date '+%d/%m/%Y %H:%M:%S') "Please put your server.cfg and steamcmd path in AutoWipeConfig.txt"   >> $PWD/AutoWipe.log
			./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "Please put your server.cfg and steamcmd path in AutoWipeConfig.txt"
  		fi
	  else
	  	echo $(date '+%d/%m/%Y %H:%M:%S') "./oxide/data/wipe.json not found"   >> $PWD/AutoWipe.log
	  fi
  else
	  echo $(date '+%d/%m/%Y %H:%M:%S') "AutoWipeConfig not found"   >> $PWD/AutoWipe.log
	  ./discord.sh --webhook-url=${arr[2]} --username "Auto wipe" --avatar "https://cdn-icons-png.flaticon.com/512/1999/1999208.png" --text "AutoWipeConfig not found"
	  echo $(date '+%d/%m/%Y %H:%M:%S') "Crate text file AutoWipeConfig.txt with your own server.cfg path 		and steamCMD location"   >> $PWD/AutoWipe.log
  fi
  ./RustDedicated -batchmode -nographics +rcon.web 1 +rcon.port 28016 +rcon.password test123
  done
  
