# Thank you for purchasing #

# BEFORE YOU READ THE REST, IF YOU NEED HELP, JOIN MY SUPPORT DISCORD... https://discord.gg/RVePam7pd7 #

# Basic Setup Walkthrough #
1. Put the DiscordLink.cs plugin on your server.
2. Fill out the config for the plugin
3. Upload the ZIP to a bot host and extract it (More info on hosting the bot below!)
4. Fill out the config for the bot (How to get some info for that is listed below)
5. Once that is done, all you need to do is press the start button on the bot

# --- [ How to | Plugin config ] --- #ß
Anywhere it asks for colors is to edit the UI. You will need to use hex percents for those sections. Editing the UI is to users descression and wont be discussed in this file.

Roles to sync. Lets say you have 3 roles you want to sync between discord and oxide groups, heres how it would look
Said roles are linked vip and legened
Get a ROLE ID by turning on discord developer mode and right clicking on the role you would like the ID of and pressing copy ID

  "Roles to Sync (Steam : Discord)": {
    "linked": "roleIDFromDiscord",
    "vip": "roleIDFromDiscord",
    "legend": "roleIDFromDiscord"
  },

  "Command To Link": "link",
  "Command To Search": "dl",
  "Command To Unlink": "unlink",

  The commands listed above can be changed. Those are what corresponds to the commands players can run in game to link, unlink, and for admins search links

  default player avatar url: You can change this URL to whatever you want. It's what is used if we are not able to get the users discord profile picture for the UI

  Oxide group for discord boosters is the group people will get in game if they boost the discord

  Use UI is done if you want to disable UI. For example if you are a community server you're not allowed to use UI and would want to disable it

# --- [ How to | Bot config ] --- #
-- Quick Info, please when you invite your bot, please invite it with the following selected on the URL generator.
- Bot, application.commands and on the permission page it's easiest to do administrator, if you dont want to do that, you will need
permission for the bot to grant roles, change nicknames, and send messages

- As well for the bot, on the bot page, please scroll to priviliged intents and enable the 3 intents on that page.

Bot token: ( https://www.writebots.com/discord-bot-token/ )
- Inviting Bot https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links

Discord server ID: Get your discord server ID by turning on discord developer mode and right clicking on your server and pressing copy ID

Steam API Key: https://steamcommunity.com/dev/apikey
Colors: You can use any hex color you'd like in those spots (https://htmlcolors.com/google-color-picker)

Permissions: Here you will put roles that you will require peeople to have in order to run those commands
ROLE_ID: Get a ROLE ID by turning on discord developer mode and right clicking on the role you would like the ID of and pressing copy ID

Linking options: As of the current version of Discord Link, you can only link VIA posting the code in a discord channel. Later on we will add the option to support slash commands for linking.

Linking channel, set this to enabled true if you want people to be able to link.
Linking channel ID, these are the channels you want people to be able to put their code in to get registered for a link
The channel ID's are gathered just like a role ID, but instead of right clicking on a role, you do it on the channel

Embed options you might need to fiddle around with this a it if you want the embed to look a certain way. But that is where you design what the embed looks that gets posted in the linking channels

Linked roles: This are the roles someone will get when they link in discord. Put role ID's here, explained how to get those above
Link logs channel: This is where the logs that someone has linked will go, put a channel ID there, explained how to get that above
Unlink logs channel: This is where the logs that someone has unlinked will go, put a channel ID there, explained how to get that above
Discord booster role: This is the ID of your discord generated booster role within your discord
Sync name to Discord - set this to true if you want players steam name to be their Discord name

Servers...
This is where you will need to put the RCON info for your server so the bot can communicate with Discord.

Server shortname: you can put whatever you want there, it's just an identifier for the server
Server IP: is the IP of your server without the port attatched
Rcon port: This is the rcon port to your server, not the app port, server port, or query port
Rcon pass: Well... It's the rcon password to your server

# --- BOT HOSTING --- # 
- There are 2 common solutions for bot hosting
- Local hosting or VPS / Bot node hosting.

  #  --- [ HOSTED SETUP ] ---  #

    My recommendation for the best bot hosting, https://botreaper.com/

    - A host system that offers Node.js 16.6 and above is required
    - All you need to do is extract the files to the file manager section of your hosting
    - Fill out the config.json 
    - Set the startup file to index.js (Most hosts have this set by default)

  #  --- [ LOCAL HOSTING SETUP ] ---  #

    - Fill out the configs
    - Make sure you have node installed. It must be version 16.6 and above.
    - Open a command prompt and do cd filePath 
    - Example, for me to get into my folder I'd do cd C:\Users\PC\Desktop\Rcon+
    - Then run node index.js

- https://www.writebots.com/discord-bot-token/
- https://discordjs.guide/preparations/adding-your-bot-to-servers.html#bot-invite-links
- Get your discord server ID by turning on discord developer mode and right clicking on your server and pressing copy ID
