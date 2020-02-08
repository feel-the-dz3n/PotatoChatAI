using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;
using static DiscordServerStatusBot.ConfigStatic;

namespace DiscordServerStatusBot
{
    class Program
    {
        static DiscordSocketClient client;
        static MinPhraseAI.PhraseAI AI;

        static void Main(string[] args)
            => MainAsync().GetAwaiter().GetResult();

        static async Task MainAsync()
        {
            Console.Title = "AI Discord Status Bot";
            
            LoadConfig();

            AI = new MinPhraseAI.PhraseAI("127.0.0.1", "minphraseglobal", "root", "");

            client = new DiscordSocketClient();
            client.Log += Client_Log;
            client.Ready += Client_Ready;

            await client.LoginAsync(Discord.TokenType.Bot, Config.BotToken);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private static Task Client_Ready()
        {
            new Thread(() => 
            {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    if (client.ConnectionState != Discord.ConnectionState.Connected)
                        continue;

                    var guild = client.GetGuild(566289126689865769);

                    guild.GetVoiceChannel(567734902234021890).ModifyAsync(c => c.Name = $"MinPhraseAI v{AI.GetVersion()}").GetAwaiter().GetResult();
                    guild.GetVoiceChannel(567735106752610315).ModifyAsync(c => c.Name = $"Count of words: {AI.MaxWord()}").GetAwaiter().GetResult();
                    guild.GetVoiceChannel(567738570547396623).ModifyAsync(c => c.Name = $"Count of phrases: {AI.MaxPhrase()}").GetAwaiter().GetResult();
                    guild.GetVoiceChannel(567738654982668288).ModifyAsync(c => c.Name = $"The newest word: {AI.GetWord(AI.MaxWord())}").GetAwaiter().GetResult();
                }
            }).Start();
            return Task.CompletedTask;
        }

        private static Task Client_Log(Discord.LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            return Task.CompletedTask;
        }
    }
}
