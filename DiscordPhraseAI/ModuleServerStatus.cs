using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static DiscordPhraseAI.ConfigStatic;

namespace DiscordPhraseAI
{
    public class ModuleServerStatus
    {
        public static void StartThread(MinPhraseAI.PhraseAI AI, DiscordSocketClient client)
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (client.ConnectionState != Discord.ConnectionState.Connected)
                            continue;

                        client.SetGameAsync($"{AI.MaxWord()} words ● {AI.MaxPhrase()} pharses ● ai.help");

                        var guild = client.GetGuild(Config.ServerStatusThing.Guild);

                        guild.GetVoiceChannel(Config.ServerStatusThing.VoiceChannel1).ModifyAsync(c => c.Name = $"MinPhraseAI v{AI.GetVersion()}").GetAwaiter().GetResult();
                        guild.GetVoiceChannel(Config.ServerStatusThing.VoiceChannel2).ModifyAsync(c => c.Name = $"Count of words: {AI.MaxWord()}").GetAwaiter().GetResult();
                        guild.GetVoiceChannel(Config.ServerStatusThing.VoiceChannel3).ModifyAsync(c => c.Name = $"Count of phrases: {AI.MaxPhrase()}").GetAwaiter().GetResult();
                        guild.GetVoiceChannel(Config.ServerStatusThing.VoiceChannel4).ModifyAsync(c => c.Name = $"The newest word: {AI.GetWord(AI.MaxWord())}").GetAwaiter().GetResult();
                        guild.GetVoiceChannel(Config.ServerStatusThing.VoiceChannel5).ModifyAsync(c => c.Name = $"Working on {client.Guilds.Count} servers").GetAwaiter().GetResult();

                        Thread.Sleep(TimeSpan.FromSeconds(15));
                    }
                    catch { }
                }
            }).Start();
        }
    }
}
