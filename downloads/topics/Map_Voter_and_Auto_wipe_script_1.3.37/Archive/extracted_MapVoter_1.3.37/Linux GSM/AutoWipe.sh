#!/bin/bash
serverfiles=$1
  echo $(date '+%d/%m/%Y %H:%M:%S')  >> "$serverfiles/AutoWipe.log"
  
  if [ -e "$serverfiles/AutoWipeConfig.txt" ]
  then
  	  wipeJson="$serverfiles/oxide/data/wipe.json";
	  if [ -e  $wipeJson ]
	  then
		isWipeDay=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.isWipeDay')
		MapSeed=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.MapSeed')
		MapSize=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.MapSize')
		#date
		FullWipe=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.FullWipe')
		ForcedWipe=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.ForcedWipe')
		iSCustomMap=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.iSCustomMap')
		LevelUrl=$(cat $serverfiles/oxide/data/wipe.json  | jq -r '.LevelUrl')
		arr=()
		while IFS= read -r line || [[ "$line" ]]; do
		arr+=("$line")
		done < $serverfiles/AutoWipeConfig.txt
		
		if [ -e ${arr[0]} ] && [ -e ${arr[1]} ]
  		then
  			if [ "$isWipeDay" == "false" ] && [ "$ForcedWipe" == "true" ]; 
  			then
				echo $(date '+%d/%m/%Y %H:%M:%S') "Updating the server"   >> $serverfiles/AutoWipe.log
				${arr[1]} +login anonymous +force_install_dir $serverfiles/ +app_update 258550  +quit
				wget https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust-linux.zip -O $serverfiles/Oxide.Rust-linux.zip
				unzip -o $serverfiles/Oxide.Rust-linux.zip
				rm $serverfiles/Oxide.Rust-linux.zip
			fi
			if [ "$isWipeDay" == "false" ];
			then
				echo $(date '+%d/%m/%Y %H:%M:%S') "Starting server without changes"   >> "$serverfiles/AutoWipe.log"
			fi
			if [ "$isWipeDay" == "true" ];
			then
				if [ "$iSCustomMap" == "false" ];
				then
					echo $(date '+%d/%m/%Y %H:%M:%S') "Changing server.seed ans server.workdsize"   >> "$serverfiles/AutoWipe.log"
					sed -i "s/.*seed=.*/seed=$MapSeed/" "${arr[0]}"
					sed -i "s/.*worldsize=.*/worldsize=$MapSize/" "${arr[0]}"
					sed -i --expression  "s@.*customlevelurl=.*@#customlevelurl= @" "${arr[0]}"
				fi
				if [ "$iSCustomMap" == "true" ];
				then
				
					sed -i --expression  "s@.*seed=.*@#seed=@" "${arr[0]}"
					sed -i --expression  "s@.*worldsize=.*@#worldsize=@" "${arr[0]}"
					#sed -i "s/.*server.levelurl.*/server.levelurl $LevelUrl/" "${arr[0]}"
					sed -i --expression  "s@.*customlevelurl=.*@customlevelurl=$LevelUrl@" "${arr[0]}"
				fi
				if [ "$FullWipe" == "true" ];
				then
					# get length of an array
					arraylength=${#arr[@]}

					# use for loop to read all values and indexes
					for (( i=2; i<${arraylength}; i++ ));
					do
						IFS=$'\n'; set -f
						for f in $(find ./server/ -name "${arr[$i]}"); 
						do 
						rm "$f";
						echo $(date '+%d/%m/%Y %H:%M:%S') "Deleting ${arr[$i]}"   >> "$serverfiles/AutoWipe.log"
						done
						unset IFS; set +f
					done
				fi
				if [ "$ForcedWipe" == "true" ]; 
	  			then
					echo $(date '+%d/%m/%Y %H:%M:%S') "Updating the server"   >> "$serverfiles/AutoWipe.log"
					${arr[1]} +login anonymous +force_install_dir $serverfiles +app_update 258550  +quit
					wget https://github.com/OxideMod/Oxide.Rust/releases/latest/download/Oxide.Rust-linux.zip -O "$serverfiles/AutoWipe.log"
					unzip -o $serverfiles/Oxide.Rust-linux.zip
					rm $serverfiles/Oxide.Rust-linux.zip
				fi
			fi
  		else
  			echo $(date '+%d/%m/%Y %H:%M:%S') "Please put your server.cfg and steamcmd path in AutoWipeConfig.txt"   >> "$serverfiles/AutoWipe.log"
  		fi
  		else
	  	echo $(date '+%d/%m/%Y %H:%M:%S') "./oxide/data/wipe.json not found"   >> "$serverfiles/AutoWipe.log"
	  fi
  else
	  echo $(date '+%d/%m/%Y %H:%M:%S') "AutoWipeConfig not found"   >> "$serverfiles/AutoWipe.log"
	  echo $(date '+%d/%m/%Y %H:%M:%S') "Crate text file AutoWipeConfig.txt with your own server.cfg path 		and steamCMD location"   >> "$serverfiles/AutoWipe.log"
  fi
  
