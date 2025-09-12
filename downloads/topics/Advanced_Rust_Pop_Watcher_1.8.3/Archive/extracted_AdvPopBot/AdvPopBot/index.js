const config = require("./config.json");

const { Client } = require("rustrcon");

const Discord = require("discord.js");

let serverStatus = [];

function initiateGlobalBot() {

  const totalPopulationBot = new Discord.Client();

  totalPopulationBot.on("ready", () => {

    setInterval(async () => {

      const totalPop = await getTotalPopulation().catch(() => null);

      if (!totalPop || totalPop == null) return;
      
      console.log(`👉 [ ${totalPopulationBot.user.tag} ] status: ${totalPop.playersOnline + totalPop.playersJoining + totalPop.playersQueued} Total players!`);

      totalPopulationBot.user.setPresence({ activity: { name: `${totalPop.playersOnline + totalPop.playersJoining + totalPop.playersQueued} Total players!`, type: "WATCHING" }, status: "online" })

    }, 10000);

  });

  totalPopulationBot.login(config.TOTAL_POP_BOT_TOKEN).then(() => {

    totalPopulationBot.user.setPresence({ activity: { name: `Establishing connection...`, type: "WATCHING" }, status: 'idle' });

    console.log(`💚 [ ${totalPopulationBot.user.tag} ] is online... Proceeding...`);

  });

}

if (config.TOTAL_POP_BOT_TOKEN) initiateGlobalBot();

config.SERVERS.forEach(async (server, index) => {

  server.rcon = new Client({
    ip: server.RCON_IP,
    port: server.RCON_PORT,
    password: server.RCON_PASS,
  });

  server.connected = false;

  server.bot = new Discord.Client();

  server.playerCountUpdate = null;

  server.scheduler = null;

  serverIP = `${server.rcon.ws.ip}:${server.rcon.ws.port}`;

  server.bot.on("ready", () => {
    console.log(`💛 [ ${server.bot.user.tag} ] is online... Going to attempt a connection to -- ${serverIP}`);
    attemptConnection();
  });

  server.rcon.on("connected", () => {

    server.connected = true;

    if(server.LOG_WHEN_SERVER_GOES_OFFLINE_AND_ONLINE) {

        const embed = new Discord.MessageEmbed()
        .setTitle(`${server.SERVER_IDENTIFIER} has gone online!`)
        .setColor(server.ONLINE_COLOR)
        .setDescription(`${server.SERVER_IDENTIFIER} has gone online...`)
        .setTimestamp()
        .setFooter(`Went online`)
    
        const channel = server.bot.channels.cache.get(server.LOG_CHANNEL_ID);
        channel.send(embed)

    }

    console.log(
      `💚 [ ${server.bot.user.tag} ] has successfully connected to -- ${serverIP}`
    );

        server.bot.user.setPresence({ activity: { name: `Establishing connection...`, type: "WATCHING" }, status: 'idle' });

    server.playerCountUpdate = setInterval(() => {

      try {

        server.rcon.send("serverinfo", "popChecker", 76457);

      } catch (err) {
        
        logError(err);
      
    }

    }, 10000);
  
});

  server.rcon.on("error", (err) => {

    logError(err.message);

  });

  function logError(err) {

    console.log(

      `[${server.bot.user.tag}] There was an issue while connecting to ${serverIP}...\n\n---------[ERROR]---------\n\n${err}\n\n-------------------------\n`
    
      );

  }

  server.rcon.on("disconnect", () => {

    if(server.LOG_WHEN_SERVER_GOES_OFFLINE_AND_ONLINE) {

        const embed = new Discord.MessageEmbed()
        .setTitle(`${server.SERVER_IDENTIFIER} has gone offline!`)
        .setColor(server.OFFLINE_COLOR)
        .setDescription(`${server.SERVER_IDENTIFIER} has gone offline...`)
        .setTimestamp()
        .setFooter(`Went offline`)
    
        const channel = server.bot.channels.cache.get(server.LOG_CHANNEL_ID);
        channel.send(embed)

    }

    clearInterval(server.playerCountUpdate);

    serverStatus[index] = { playersOnline: 0, queuedPlayers: 0, joiningPlayers: 0 };

    if (server.connected) {

        server.bot.user.setPresence({ activity: { name: `${server.SERVER_OFFLINE_MESSAGE}`, type: "WATCHING" }, status: 'dnd' });

        server.connected = false;

        console.log(

          `❤️ [ ${server.bot.user.tag} ] has dropped connection to -- ${serverIP}`

        );

    } else {

      console.log(

        `💛 [ ${server.bot.user.tag} ] attempting to connect to -- ${serverIP}`

      );

    }

    attemptConnection();
  });



  server.rcon.on("message", async (message) => {
    const mType = message["Type"];
    const mContent = message["content"];
    const mIdentifier = message["Identifier"];

    switch (mType) {
      case "Generic":
        if (mIdentifier === 76457) {
          const playersOnline = mContent["Players"];
          const maxPlayers = mContent["MaxPlayers"];
          const queuedPlayers = mContent["Queued"];
          const joiningPlayers = mContent["Joining"];
          const fps = mContent["Framerate"];

          serverStatus[index] = {
            playersOnline,
            queuedPlayers,
            joiningPlayers,
          };

          if(server.ENABLE_THRESHOLD) {

            let thresholdPercent = `0.${server.THRESHOLD_PERCENT}`;
            threshold = maxPlayers * thresholdPercent;

          } 

          if(server.ENABLE_THRESHOLD && playersOnline < threshold) {
              
            server.bot.user.setPresence({ activity: { name: `${server.THRESHOLD_MESSAGE}`, type: "WATCHING" }, status: "online" });
            console.log(`👉 [ ${server.bot.user.tag} ] status: ${server.THRESHOLD_MESSAGE}`);

          } else if(queuedPlayers > 0) {

            let popMessage = server.PLAYERS_QUEUED_MESSAGE.replace(/playersOnline/gi, playersOnline);
            popMessage = popMessage.replace(/maxPlayers/gi, maxPlayers);
            popMessage = popMessage.replace(/joiningPlayers/gi, joiningPlayers);
            popMessage = popMessage.replace(/queuedPlayers/gi, queuedPlayers);

            server.bot.user.setPresence({ activity: { name: `${popMessage}`, type: "WATCHING" }, status: "online" });
            console.log(`👉 [ ${server.bot.user.tag} ] status: ${popMessage}`);

          } else if(joiningPlayers > 0) {

            let popMessage = server.PLAYERS_JOINING_MESSAGE.replace(/playersOnline/gi, playersOnline);
            popMessage = popMessage.replace(/maxPlayers/gi, maxPlayers);
            popMessage = popMessage.replace(/joiningPlayers/gi, joiningPlayers);
            popMessage = popMessage.replace(/queuedPlayers/gi, queuedPlayers);

            server.bot.user.setPresence({ activity: { name: `${popMessage}`, type: "WATCHING" }, status: "online" });
            console.log(`👉 [ ${server.bot.user.tag} ] status: ${popMessage}`);

          } else {

            let popMessage = server.PLAYER_COUNT_MESSAGE.replace(/playersOnline/gi, playersOnline);
            popMessage = popMessage.replace(/maxPlayers/gi, maxPlayers);
            popMessage = popMessage.replace(/joiningPlayers/gi, joiningPlayers);
            popMessage = popMessage.replace(/queuedPlayers/gi, queuedPlayers);

            server.bot.user.setPresence({ activity: { name: `${popMessage}`, type: "WATCHING" }, status: "online" });
            console.log(`👉 [ ${server.bot.user.tag} ] status: ${popMessage}`);

          }

          if(server.ENABLE_DYNAMIC_POP_CHANGER) {
          
            const popIncreaseNumber = `${maxPlayers - server.INCREASE_POP_IF_THIS_MANY_PLAYERS_AWAY_FROM_MAX_PLAYERS}`
            const popDecreaseNumber = `${maxPlayers - server.DECREASE_POP_IF_THIS_MANY_PLAYERS_AWAY_FROM_MAX_PLAYERS}`;
            const popIncreasePlayerMax = parseInt(maxPlayers) + parseInt(server.INCREASE_BY);
            const popDecreasePlayerMax = `${maxPlayers - server.DECREASE_BY}`;
            const startingPlayerMax = server.STARTING_PLAYER_CAP;
            const maxPlayerCap = server.MAX_PLAYER_CAP;
            const fpsLimit = server.DO_NOT_INCREASE_PLAYER_CAP_IF_SERVER_IS_ON_LESS_THAN_THIS_AMOUNT_OF_FPS;


            if(playersOnline >= popIncreaseNumber && fps > fpsLimit) {

              if(maxPlayerCap == popIncreasePlayerMax) {

                server.rcon.send(`server.maxplayers ${maxPlayerCap}`, "popChanger", 25612);

              } else if(popIncreasePlayerMax > maxPlayerCap) {

                server.rcon.send(`server.maxplayers ${maxPlayerCap}`, "popChanger", 25612);

              } else {

                server.rcon.send(`server.maxplayers ${popIncreasePlayerMax}`, "popChanger", 25612);

                if(server.LOG_WHEN_POP_CAP_GETS_CHANGED) {

                  const channel = server.bot.channels.cache.get(server.POP_CAP_LOG_CHANNEL_ID);
  
                  const embed = new Discord.MessageEmbed()
                    .setAuthor(`${mContent["Hostname"]}`)
                    .setDescription(`Pop changed from ${maxPlayers} -> ${popIncreasePlayerMax}`)
                    .setTimestamp()
                    .setFooter(`Pop increased`)
                    .setColor(server.INCREASE_COLOR)
  
                  channel.send(embed);
  
                } 

              } 

            } else if(playersOnline <= popDecreaseNumber) {

              if(startingPlayerMax == popDecreasePlayerMax) {

                server.rcon.send(`server.maxplayers ${startingPlayerMax}`, "popChanger", 25612);

              } else if(popDecreasePlayerMax < startingPlayerMax) {

                server.rcon.send(`server.maxplayers ${startingPlayerMax}`, "popChanger", 25612);

              } else {

                server.rcon.send(`server.maxplayers ${popDecreasePlayerMax}`, "popChanger", 25612);

                if(server.LOG_WHEN_POP_CAP_GETS_CHANGED) {
  
                  const channel = server.bot.channels.cache.get(server.POP_CAP_LOG_CHANNEL_ID);
  
                  const embed = new Discord.MessageEmbed()
                    .setAuthor(`${mContent["Hostname"]}`)
                    .setDescription(`Pop changed from ${maxPlayers} -> ${popDecreasePlayerMax}`)
                    .setTimestamp()
                    .setFooter(`Pop decreased`)
                    .setColor(server.DECREASE_COLOR)
  
                  channel.send(embed);

                }

              }

            } else {

              return;

            }

          }

        }

        break;
    }
  });

  function attemptConnection() {

    console.log(
      `💛 [ ${server.bot.user.tag} ] Attempting to connect to ${serverIP}`
    );

    server.rcon.login();

  }
  
  server.bot.login(server.BOT_TOKEN);

});

async function getTotalPopulation() {

  return new Promise((res, rej) => {

    if (serverStatus.length === 0) rej("No servers yet exist in the array!");

    let totalData = { playersOnline: 0, playersQueued: 0, playersJoining: 0 };

    serverStatus.forEach(({ playersOnline, queuedPlayers, joiningPlayers }, index, array) => {
      totalData.playersOnline += playersOnline;
      totalData.playersQueued += queuedPlayers;
      totalData.playersJoining += joiningPlayers;

      if (index === array.length - 1) res(totalData);

    });
    
  });
}