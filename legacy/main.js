/*
EXTEND REMASTERED
*/

const http = require("http");
const Discord = require("discord.js");

const intent = new Discord.Intents(["GUILDS","GUILD_MESSAGES","GUILD_MEMBERS"]);
const client = new Discord.Client({
  intents: intent,
});