using Discord.WebSocket;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using static DiscordPhraseAI.ConfigStatic;
using MinPhraseAI;
using System.IO;
using System.Reflection;

namespace DiscordPhraseAI
{
    class Program
    {
        private const string Token2 = "Discord AI Bot";
        public static DiscordSocketClient client = new DiscordSocketClient();
        static MinPhraseAI.PhraseAI AI;
        public static List<ulong> SpamChannels = new List<ulong>();

        static void Main(string[] args)
        {
            Console.Title = "Discord Phrase AI Bot";

            ConfigStatic.LoadConfig();
            ConfigStatic.InitSaveThread();

            new Thread(() =>
            {
                Console.WriteLine("Connecting to database...");

                AI = new MinPhraseAI.PhraseAI("127.0.0.1", "minphraseglobal", "root", "");
                AI.LogLine += AI_LogLine;
                AI.BotMisunderstand += AI_BotMisunderstand;

                Console.WriteLine("Database connected!");
            }).Start();

            Console.WriteLine("Starting Discord connection...");
            StartConnection().GetAwaiter().GetResult();
        }

        public static void AddSpamChannel(ulong id)
        {
            SpamChannels.Add(id);
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMinutes(Config.ChannelSpamTime));

                if(SpamChannels.Contains(id))
                    SpamChannels.Remove(id);
            }).Start();
        }

        private static bool ThereIsDiscord(string[] owners)
        {
            foreach (var owner in owners)
                if (owner.StartsWith(Token2))
                    return true;

            return false;
        }

        private static ulong[] GetDiscordOwner(string[] owners)
        {
            List<string> o = new List<string>();

            foreach (var owner in owners)
                if (owner.StartsWith(Token2))
                    o.Add(owner.Remove(0, Token2.Length + 1));

            string[] args = o[o.Count - 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            ulong own = ulong.Parse(args[0]);
            ulong guild = ulong.Parse(args[1]);
            ulong channel = ulong.Parse(args[2]);

            return new ulong[] { own, guild, channel };
        }

        private static void AI_BotMisunderstand(MinPhraseAI.ImportantThings.MisunderstandType type, string[] owners, string last_owner, params MinPhraseAI.BasicDBEntry[] something)
        {
            try
            {
                if (!ThereIsDiscord(owners))
                    return;

                var OwnerThing = GetDiscordOwner(owners);
                ulong owner = OwnerThing[0];
                ulong original_guild = OwnerThing[1];
                ulong original_channel = OwnerThing[2];

                if (SpamChannels.Contains(original_channel))
                    return;

                if (Config.DoNotDisturb.Contains(owner)
                    || Config.BannedUsers.Contains(owner)
                    || !Config.InteractOnServers.Contains(original_guild))
                    return;
                
                AddSpamChannel(original_channel);

                if (type == ImportantThings.MisunderstandType.WordSimilarity)
                {
                    var channel = (ISocketMessageChannel)client.GetChannel(original_channel);
                    var WordA = something[0] as Word;
                    var WordB = something[1] as Word;

                    Console.WriteLine($"Asking similarity '{WordA}' & '{WordB}' in channel '{channel.Name}'");

                    DiscordSelector question = new DiscordSelector(
                        $"{DiscordSelector.DefaultYes} - Word ``{WordA}`` is the initial of the word ``{WordB}``\r\n" +
                        $"{DiscordSelector.DefaultVS} - Word ``{WordB}`` is the initial of the word ``{WordA}``\r\n" +
                        $"{DiscordSelector.DefaultNo} - These words can't be compared",
                       new string[] { DiscordSelector.DefaultYes, DiscordSelector.DefaultVS, DiscordSelector.DefaultNo },
                       (ISocketMessageChannel)client.GetChannel(original_channel),
                       null,
                       25);

                    question.UserMadeChoice += (s, reaction) =>
                    {
                        s.Message.DeleteAsync();

                        if (s.User != null)
                            Console.WriteLine($"Similarity '{WordA}' & '{WordB}' {s.User.Username}");

                        if (reaction == DiscordSelector.DefaultYes)
                            AI.AddSimilar(WordA.ID, WordB.ID);
                        else if (reaction == DiscordSelector.DefaultVS)
                            AI.AddSimilar(WordB.ID, WordA.ID);
                        else if (reaction == DiscordSelector.DefaultNo)
                            AI.WordSimilarDeny(WordA.ID, WordB.ID);
                    };

                    question.Send();
                }
                else if (type == MinPhraseAI.ImportantThings.MisunderstandType.WordProperties)
                {
                    MinPhraseAI.Word word = something[0] as MinPhraseAI.Word;
                    StringBuilder intro = new StringBuilder();

                    Console.WriteLine($"Asking misword '{word.Value}' to {client.GetUser(owner).Username} ({owner})");

                    DiscordSelector question = new DiscordSelector($"Hey, {client.GetUser(owner).Username}, could you help me to understand word ``{word}``? P.S.: Hit 🛑 and I will not disturb you anymore.\r\nThis message is allowed by server's administration. To stop this send ``ai.interact``.",
                        new string[] { DiscordSelector.DefaultYes, DiscordSelector.DefaultNo, "🛑" },
                        (ISocketMessageChannel)client.GetChannel(original_channel),
                        client.GetUser(owner),
                        25);

                    question.UserMadeChoice += (s, reaction) =>
                    {
                        s.Message.DeleteAsync();

                        if (reaction == "🛑")
                        {
                            Config.DoNotDisturb.Add(s.User.Id);
                            return;
                        }

                        if (reaction != DiscordSelector.DefaultYes)
                            return;

                        var channel = client.GetUser(owner).GetOrCreateDMChannelAsync().GetAwaiter().GetResult();

                        // owner = 261121299680591873;


                        Console.WriteLine($"Sending misword '{word.Value}' to {channel.Name} ({owner})");

                        intro.AppendLine($"Hello. I am Intelligence of Plastic, but you can call me a dictionary. If you can help me with word ``{word}``, then send ``ai.word {word}`` here.");
                        intro.AppendLine("Other commands:");
                        intro.AppendLine("Send ``ai.help`` to get help and all available commands.");
                        intro.AppendLine("Send ``ai.stop`` and bot will never disturb you.");

                        channel.SendMessageAsync(intro.ToString());
                    };
                    question.Send();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void AI_LogLine(string text)
        {
            Console.WriteLine("[AI] " + text);
        }

        static async Task StartConnection()
        {
            client.JoinedGuild += Client_JoinedGuild;
            client.Log += Client_Log;
            client.Ready += Client_Ready;
            client.MessageReceived += Client_MessageReceived;
            await client.LoginAsync(Discord.TokenType.Bot, Config.BotToken);
            await client.StartAsync();

            await client.SetGameAsync("ai.help", null, Discord.ActivityType.Streaming);

            await Task.Delay(-1);
        }

        private static Task Client_JoinedGuild(SocketGuild arg)
        {
            arg.Owner.GetOrCreateDMChannelAsync().GetAwaiter().GetResult().SendMessageAsync(
                "Thank you for inviting me to your server. If you want me to interact with people, go to your server and write ``ai.interact`` somewhere."
                );

            return Task.CompletedTask;
        }

        private static Task Client_Log(Discord.LogMessage arg)
        {
            Console.WriteLine(arg.ToString());
            return Task.CompletedTask;
        }

        private static Task Client_MessageReceived(SocketMessage arg)
        {
            var channel = arg.Channel as SocketGuildChannel;

            StringBuilder str = new StringBuilder();

            if (arg.Author.Id == client.CurrentUser.Id)
                return Task.CompletedTask;

            Console.Write($" > {arg.Author.Username} ({arg.Author.Id})");
            if (channel != null)
                Console.Write(" on " + channel.Guild.Name);

            Console.WriteLine(":");

            Console.WriteLine($"   {arg.Content}");

            if (channel != null && channel.Guild.Id == 287521695487623168 && arg.Content.Contains(".pick")) // mwo
                arg.Channel.SendMessageAsync(".pick");

            if (Config.BannedUsers.Contains(arg.Author.Id))
            {
                Console.WriteLine("(user is banned)");
                return Task.CompletedTask;
            }

            if (!arg.Author.IsBot)
            {
                if (Config.AdminUsers.Contains(arg.Author.Id))
                {
                    if (arg.Content.ToLower() == "ai.wipe")
                    {
                        var p = AI.MaxPhrase();
                        var w = AI.MaxWord();

                        DiscordSelector sel = new DiscordSelector($"<@{arg.Author.Id}>, are you sure that you want to remove {w} words and {p} phrases?", arg.Channel, arg.Author, 5);
                        sel.UserMadeChoice += (a, reaction) =>
                        {
                            a.Message.DeleteAsync();

                            if (reaction == DiscordSelector.DefaultYes)
                            {
                                var msg = arg.Channel.SendMessageAsync("Database is wiping...").GetAwaiter().GetResult();

                                AI?.DeleteEverything(true);

                                msg.ModifyAsync(m => m.Content = $"Removed {p} phrases and {w} words.");
                            }
                        };
                        sel.Send();

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower() == "ai.delowners")
                    {
                        var p = AI.MaxPhrase();
                        var w = AI.MaxWord();

                        DiscordSelector sel = new DiscordSelector($"<@{arg.Author.Id}>, are you sure that you want to remove ALL owners for {w} words and {p} phrases?", arg.Channel, arg.Author, 5);
                        sel.UserMadeChoice += (a, reaction) =>
                        {
                            a.Message.DeleteAsync();

                            if (reaction == DiscordSelector.DefaultYes)
                            {
                                var msg = arg.Channel.SendMessageAsync("Woorking.......").GetAwaiter().GetResult();

                                AI?.DeleteOwners(true);

                                msg.ModifyAsync(m => m.Content = $"Removed owners for {p} phrases and {w} words.");
                            }
                        };
                        sel.Send();

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.wipetable "))
                    {
                        string table = arg.Content.Split(' ')[1];
                        var p = AI.CountOf(table);

                        DiscordSelector sel = new DiscordSelector($"<@{arg.Author.Id}>, are you sure that you want to WIPE table ``{table}``? It has {p} rows.", arg.Channel, arg.Author, 5);
                        sel.UserMadeChoice += (a, reaction) =>
                        {
                            a.Message.DeleteAsync();

                            if (reaction == DiscordSelector.DefaultYes)
                            {
                                var msg = arg.Channel.SendMessageAsync("Woorking.......").GetAwaiter().GetResult();

                                AI?.Command($"TRUNCATE TABLE {table};");

                                msg.ModifyAsync(m => m.Content = $"Table ``{table}`` truncated (-{p} rows).");
                            }
                        };
                        sel.Send();

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower() == "ai.debug")
                    {
                        var msg = arg.Channel.SendMessageAsync("Connecting to the local debugger...").GetAwaiter().GetResult();

                        Debugger.Launch();

                        if (Debugger.IsAttached)
                            msg.ModifyAsync(m => m.Content = "Connected to the local debugger.");
                        else
                            msg.DeleteAsync();

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower() == "ai.exit")
                    {
                        arg.Channel.SendMessageAsync("gg wp").GetAwaiter().GetResult();
                        client.SetStatusAsync(Discord.UserStatus.Invisible).GetAwaiter().GetResult();
                        Environment.Exit(0);
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.block "))
                    {
                        ulong usr = ulong.Parse(arg.Content.Split(' ')[1]);
                        var user = client.GetUser(usr);

                        if (Config.BannedUsers.Contains(usr))
                        {
                            arg.Channel.SendMessageAsync($"User {user.Username} unblocked");
                            Config.BannedUsers.Remove(usr);
                        }
                        else
                        {
                            arg.Channel.SendMessageAsync($"User {user.Username} blocked");
                            Config.BannedUsers.Add(usr);
                        }

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.ver "))
                    {
                        int val = int.Parse(arg.Content.Split(' ')[1]);

                        AI.Settings.Set("use_only_verified", val);
                        arg.Channel.SendMessageAsync("use_only_verified -> " + AI.Settings.GetBool("use_only_verified"));

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.minsim "))
                    {
                        double val = double.Parse(arg.Content.Split(' ')[1]);

                        AI.Settings.Set("min_word_similarity", val.ToString());
                        arg.Channel.SendMessageAsync("min_word_similarity -> " + AI.Settings.GetString("min_word_similarity"));

                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.spamtime "))
                    {
                        int val = int.Parse(arg.Content.Split(' ')[1]);

                        Config.ChannelSpamTime = val;
                        arg.Channel.SendMessageAsync($"Interacting every {val} minutes in one channel.");

                        SpamChannels.Clear();
                        
                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.delword "))
                    {
                        string wordstr = arg.Content.Split(' ')[1];

                        var wordidx = AI.WordIndex(wordstr);
                        if (wordidx == -1)
                        {
                            arg.Channel.SendMessageAsync($"Word not found.");
                            return Task.CompletedTask;
                        }

                        AI.DeleteWord(wordidx);
                        arg.Channel.SendMessageAsync($"Word removed.");
                        return Task.CompletedTask;
                    }
                    else if (arg.Content.ToLower().StartsWith("ai.delphrase "))
                    {
                        string wordstr = arg.Content.Remove(0, "ai.delphrase ".Length);

                        var wordidx = AI.PhraseIndex(wordstr);
                        if (wordidx == -1)
                        {
                            arg.Channel.SendMessageAsync($"Phrase not found.");
                            return Task.CompletedTask;
                        }

                        AI.DeletePhrase(wordidx);
                        arg.Channel.SendMessageAsync($"Phrase removed.");
                        return Task.CompletedTask;
                    }
                }

                if (arg.Content.ToLower().StartsWith("ai.learn fuck"))
                {
                    if (arg.Author is SocketGuildUser)
                    {
                        var user = arg.Author as SocketGuildUser;
                        if (user.GuildPermissions.Administrator || Config.AdminUsers.Contains(user.Id))
                        {
                            var LearnSelector = new DiscordSelector(
                                $"<@{arg.Author.Id}>, bot will start learning words and phrases from users from this server. Do you really want it?",
                                arg.Channel, arg.Author);
                            LearnSelector.UserMadeChoice += LearnSelector_UserMadeChoice;
                            LearnSelector.Send();
                        }
                        else
                        {
                            arg.Channel.SendMessageAsync("Administrator role required.");
                        }
                    }
                    else arg.Channel.SendMessageAsync("Can't use this command on current server.");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.wordsim "))
                {
                    string a = arg.Content.ToLower().Split(' ')[1];
                    string b = arg.Content.ToLower().Split(' ')[2];

                    arg.Channel.SendMessageAsync($"Word A: ``{a}``, Word B: ``{b}``. Similarity: {MinPhraseAI.SynonymsClass.CalculateSimilarity(a, b)}");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.testque"))
                {
                    DiscordSelector sel = new DiscordSelector("Choose something",
                        new string[]
                        {
                            DiscordSelector.DefaultYes, DiscordSelector.DefaultNo, DiscordSelector.DefaultKoKo,
                            DiscordSelector.DefaultStop, DiscordSelector.DefaultLeft, DiscordSelector.DefaultRight,
                            DiscordSelector.DefaultUp, DiscordSelector.DefaultDown, DiscordSelector.DefaultNumber[0],
                            DiscordSelector.DefaultNumber[1], DiscordSelector.DefaultNumber[2]
                        },
                        arg.Channel, null, 25);
                    sel.UserMadeChoice += (a, reaction) =>
                    {
                        a.Message.DeleteAsync();
                        arg.Channel.SendMessageAsync($"User ``{a.User.Username}``, choice is: {reaction}");
                    };
                    sel.Send();
                }
                else if (arg.Content.ToLower().StartsWith("ai.testlist"))
                {
                    List<string> combs = new List<string>();

                    for (int i = 0; i < 25; i++)
                        combs.Add(AI.Generate.WordCombination());

                    SelectorList list = new SelectorList($"<@{arg.Author.Id}>, choose something:", arg.Channel, arg.Author);
                    list.UserMadeChoice += (a, b) =>
                    {
                        var choice = combs.IndexOf((string)b.Value);
                        arg.Channel.SendMessageAsync($"<@{a.Selector.User.Id}>, your choice is: ``[{choice}] {combs[choice]}``");
                    };

                    foreach (var c in combs)
                        list.Items.Add(new SelectorItem(c + " (item)", c));

                    list.Send();

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.interact"))
                {
                    if (arg.Author is SocketGuildUser)
                    {
                        var user = arg.Author as SocketGuildUser;
                        if (user.GuildPermissions.Administrator || Config.AdminUsers.Contains(user.Id))
                        {
                            var InteractSelector = new DiscordSelector(
                                $"<@{arg.Author.Id}>, bot will start interacting with users, like sending DMs with help to determine words meaning or sometimes asking the same between the messages of people on the server. Do you really want it?",
                                arg.Channel, arg.Author);
                            InteractSelector.UserMadeChoice += InteractSelector_UserMadeChoice;
                            InteractSelector.Send();
                        }
                        else
                        {
                            arg.Channel.SendMessageAsync("Administrator role required.");
                        }
                    }
                    else arg.Channel.SendMessageAsync("Can't use this command on current server.");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.15words")
                {
                    var w = AI.Stuff.TopWords(15);

                    str.AppendLine("Most used words:");

                    for (int i = 0; i < w.Length; i++)
                        str.AppendLine($"#{i + 1} ``{w[i].Value}`` (ID: {w[i].ID}, Usage Count: {w[i].UsageCount})");

                    arg.Channel.SendMessageAsync(str.ToString());

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.15phrases")
                {
                    var w = AI.Stuff.TopPhrases(15);

                    str.AppendLine("Most used phrases:");

                    for (int i = 0; i < w.Length; i++)
                        str.AppendLine($"#{i + 1} ``{w[i].Value}`` (ID: {w[i].ID}, Usage Count: {w[i].UsageCount})");

                    arg.Channel.SendMessageAsync(str.ToString());

                    return Task.CompletedTask;
                }

                else if (arg.Content.ToLower() == "ai.wordnoprop")
                {
                    var w = AI.Stuff.WordsWithoutProperties(15);

                    str.AppendLine("Words without properties:");

                    for (int i = 0; i < w.Count; i++)
                        str.AppendLine($"#{i + 1} ``{w[i].Value}`` (ID: {w[i].ID}, Usage Count: {w[i].UsageCount})");

                    arg.Channel.SendMessageAsync(str.ToString());

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.randsim")
                {
                    var msg = arg.Channel.SendMessageAsync("Searching for similar words...").GetAwaiter().GetResult();

                    new Thread(() =>
                    {
                        for (int i = 0; i < 200; i++)
                        {
                            var word = AI.RandomWord();

                            if (!AI.WordHasSimilar(word.ID))
                            {
                                var sim = AI.GetSimilarWord(word.ID);
                                if (sim != -1)
                                {
                                    if (SpamChannels.Contains(arg.Channel.Id))
                                        SpamChannels.Remove(arg.Channel.Id);

                                    var own = GetOwner(arg.Author, arg.Channel);
                                    AI_BotMisunderstand(ImportantThings.MisunderstandType.WordSimilarity,
                                        new string[] { own },
                                        own,
                                        word, AI.GetWord(sim));

                                    msg.DeleteAsync();

                                    return;
                                }
                            }
                        }
                        msg.ModifyAsync(m => m.Content = $"{DiscordSelector.DefaultNo} Can't find similar words. Try again.");
                    }).Start();
                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.stats")
                {
                    var msg = arg.Channel.SendMessageAsync("Loading...").GetAwaiter().GetResult();

                    string created = AI.Settings.GetString("wipe_date");
                    if (created == null) created = "Unknown";

                    str.AppendLine($"Total words: {AI.MaxWord()}");
                    str.AppendLine($"  The newest one: ``{AI.GetWord(AI.MaxWord()).Value}``");
                    str.AppendLine($"Total phrases: {AI.MaxPhrase()}");
                    str.AppendLine($"  The newest one: ``{AI.GetPhrase(AI.MaxPhrase()).Value}``");
                    str.AppendLine();
                    str.AppendLine($"Min. word similarity: {AI.Settings.GetString("min_word_similarity")}");
                    str.AppendLine($"Use only verified: {AI.Settings.GetBool("use_only_verified")}");
                    str.AppendLine($"Database wiped at: {created}");
                    str.AppendLine($"AI's source code has {GetSourceLines()} lines!");

                    str.AppendLine();
                    str.AppendLine($"Interacting every {Config.ChannelSpamTime} minutes in one channel.");

                    if (channel != null)
                    {
                        if (SpamChannels.Contains(channel.Id))
                            str.AppendLine($"(Already interacted in this channel, waiting)");

                        str.AppendLine($"Interact on this server: {Config.InteractOnServers.Contains(channel.Guild.Id)}");
                    }

                    str.AppendLine();
                    str.AppendLine($"MinPhraseAI v{AI.GetVersion()} (C) Dz3n");

                    msg.ModifyAsync(m => m.Content = str.ToString());

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.wordcomb "))
                {
                    var args = arg.Content.ToLower().Split(' ');
                    var wordv = args[1];
                    int len = args.Length >= 3 ? int.Parse(args[2]) - 1 : 1;

                    if (len + 1 > 50)
                        return Task.CompletedTask;

                    var msg = arg.Channel.SendMessageAsync($"Generating {len + 1} combinations for ``{wordv}``...").GetAwaiter().GetResult();

                    new Thread(() =>
                    {
                        var word = AI.WordIndex(wordv);

                        msg.ModifyAsync(m => m.Content = "```" + AI.Generate.WordCombination(word, len) + "```");
                    }).Start();

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.wordcomb"))
                {
                    arg.Channel.SendMessageAsync("```" + AI.Generate.WordCombination() + "```");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.randwordcomb "))
                {
                    int max = int.Parse(arg.Content.Split(' ')[1].Trim());

                    if (max > 75)
                        return Task.CompletedTask;

                    var msg = arg.Channel.SendMessageAsync("Generating...").GetAwaiter().GetResult();
                    int every = max / 3;

                    new Thread(() =>
                    {
                        str.AppendLine("```");

                        for (int i = 0, repeat = 0; i < max; i++, repeat++)
                        {
                            if (repeat > every)
                            {
                                msg.ModifyAsync(m => m.Content = $"Generating [{i + 1}/{max}]...").GetAwaiter().GetResult();
                                repeat = 0;
                            }

                            str.AppendLine($"[{i + 1}] {AI.Generate.WordCombination()}");
                        }
                        str.AppendLine("```");

                        msg.ModifyAsync(m => m.Content = str.ToString());
                    }).Start();
                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.question "))
                {
                    arg.Channel.SendMessageAsync("```" + AI.Generate.Question(arg.Content.Remove(0, "AI.Question ".Length)) + "```");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.invite")
                {
                    arg.Channel.SendMessageAsync("Invite me:\r\nhttps://discordapp.com/oauth2/authorize?&client_id=543103130091520011&scope=bot&permissions=0");
                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower() == "ai.stop")
                {
                    if (!Config.DoNotDisturb.Contains(arg.Author.Id))
                    {
                        Config.DoNotDisturb.Add(arg.Author.Id);
                        arg.Channel.SendMessageAsync("You will not receive any notifications anymore. Send ``ai.stop`` again to enable it.");
                    }
                    else
                    {
                        Config.DoNotDisturb.Remove(arg.Author.Id);
                        arg.Channel.SendMessageAsync("You will receive notifications again.");
                    }

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.denysim"))
                {
                    string[] words = arg.Content.ToLower().Split(' ');

                    var a = AI.WordIndex(words[1]);
                    var b = AI.WordIndex(words[2]);

                    AI.WordSimilarDeny(a, b);

                    arg.Channel.SendMessageAsync($"Words ``{words[1]}`` and ``{words[2]}`` can't be similar.");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.dosim"))
                {
                    string[] words = arg.Content.ToLower().Split(' ');

                    var a = AI.WordIndex(words[1]);
                    var b = AI.WordIndex(words[2]);

                    AI.AddSimilar(a, b);

                    arg.Channel.SendMessageAsync($"Word ``{words[1]}`` is the initial of the word ``{words[2]}`` now.");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.delsim"))
                {
                    string[] words = arg.Content.ToLower().Split(' ');

                    var a = AI.WordIndex(words[1]);
                    var b = AI.WordIndex(words[2]);

                    AI.RemoveSimilar(a, b);

                    arg.Channel.SendMessageAsync($"Words ``{words[1]}`` and ``{words[2]}`` are not similar now.");

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.word"))
                {
                    string[] args = arg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (args.Length < 2)
                        return Task.CompletedTask;

                    var word_idx = AI.WordIndex(args[1]);

                    if (word_idx == -1)
                    {
                        arg.Channel.SendMessageAsync("There's no such word");
                        return Task.CompletedTask;
                    }

                    var word = AI.GetWord(word_idx);
                    var props_i = AI.GetWordProps(word_idx);

                    str.Append($"<@{arg.Author.Id}>, word: ``{word}`` (ID: {word.ID}) ");

                    if (props_i.Count >= 1)
                    {
                        str.AppendLine($"has {props_i.Count} properties:");

                        for (int i = 0; i < props_i.Count; i++)
                        {
                            str.Append("``" + ImportantThings.GetFriendlyName(props_i[i].ToString()) + "``");

                            if (i != props_i.Count - 1)
                                str.Append(", ");
                        }

                        str.AppendLine();
                    }
                    else
                    {
                        str.AppendLine("has **no** properties.");
                    }

                    bool hasi = AI.WordHasInitial(word_idx);
                    if (hasi)
                    {
                        var initial = AI.GetWord(AI.GetWordInitial(word_idx));
                        str.AppendLine($"The initial word is: ``{initial}`` (ID: {initial.ID})");
                    }

                    List<string> emojis = new List<string>();

                    str.AppendLine();
                    str.AppendLine($"What would you like to do with word ``{word}``?");

                    str.AppendLine(DiscordSelector.DefaultA + " - add property...");
                    emojis.Add(DiscordSelector.DefaultA);

                    if (props_i.Count >= 1)
                    {
                        str.AppendLine(DiscordSelector.DefaultD + " - delete property...");
                        emojis.Add(DiscordSelector.DefaultD);
                    }

                    if (hasi)
                    {
                        str.AppendLine(DiscordSelector.DefaultI + " - remove initial of this word.");
                        emojis.Add(DiscordSelector.DefaultI);

                        str.AppendLine(DiscordSelector.DefaultS + " - swap initial and this word.");
                        emojis.Add(DiscordSelector.DefaultS);
                    }

                    str.AppendLine(DiscordSelector.DefaultNo + " - cancel.");
                    emojis.Add(DiscordSelector.DefaultNo);

                    DiscordSelector YesNo = new DiscordSelector(str.ToString(), emojis.ToArray(), arg.Channel, arg.Author, 60);
                    YesNo.UserMadeChoice += (s, reaction) =>
                    {
                        s.Message.DeleteAsync();

                        if (reaction == DiscordSelector.DefaultA)
                        {
                            SelectorList PropList = new SelectorList($"<@{arg.Author.Id}>, choose one property", arg.Channel, arg.Author);

                            var values = Enum.GetValues(typeof(ImportantThings.WordProp));

                            foreach (var value in values)
                                if (!props_i.Contains((ImportantThings.WordProp)value))
                                    PropList.Items.Add(new SelectorItem(ImportantThings.GetFriendlyName(value.ToString()), value));

                            PropList.UserMadeChoice += (l, choice) =>
                            {
                                AI.AddWordProp(word.ID, (ImportantThings.WordProp)choice.Value);
                                arg.Channel.SendMessageAsync($"<@{arg.Author.Id}>, you *added* property ``{choice.Name}`` to the word ``{word}``!");
                            };

                            PropList.Send();
                        }
                        else if (reaction == DiscordSelector.DefaultD)
                        {
                            SelectorList PropList = new SelectorList($"<@{arg.Author.Id}>, choose one property", arg.Channel, arg.Author);

                            foreach (var value in props_i)
                                PropList.Items.Add(new SelectorItem(ImportantThings.GetFriendlyName(value.ToString()), value));

                            PropList.UserMadeChoice += (l, choice) =>
                            {
                                AI.RemoveWordProp(word.ID, (ImportantThings.WordProp)choice.Value);
                                arg.Channel.SendMessageAsync($"<@{arg.Author.Id}>, you *removed* property ``{choice.Name}`` from the word ``{word}``!");
                            };

                            PropList.Send();
                        }
                        else if (reaction == DiscordSelector.DefaultI)
                        {
                            var initial = AI.GetWord(AI.GetWordInitial(word_idx));

                            AI.RemoveInitialForWord(word_idx);
                            arg.Channel.SendMessageAsync($"<@{arg.Author.Id}>, now ``{initial}`` is not the initial of the word ``{word}``.");
                        }
                        else if (reaction == DiscordSelector.DefaultS)
                        {
                            var initial = AI.GetWord(AI.GetWordInitial(word_idx));

                            AI.RemoveInitialForWord(word_idx);
                            var result = AI.AddSimilar(word_idx, initial.ID);

                            if (result)
                                arg.Channel.SendMessageAsync($"<@{arg.Author.Id}>, now ``{word}`` is the initial of the word ``{initial}``.");
                            else
                                arg.Channel.SendMessageAsync($"<@{arg.Author.Id}>, error happened.");
                        }
                    };
                    YesNo.Send();

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.api"))
                {
                    string line =
                        arg.Content.Length >= "ai.api ".Length ?
                        arg.Content.ToLower().Remove(0, "ai.api ".Length) :
                        "";

                    var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance;

                    List<MethodInfo> methods = new List<MethodInfo>();
                    methods.AddRange(AI.GetType().GetMethods(flags));
                    methods.AddRange(AI.Settings.GetType().GetMethods(flags));
                    methods.AddRange(AI.Stuff.GetType().GetMethods(flags));
                    methods.AddRange(AI.Generate.GetType().GetMethods(flags));

                    for (int i = 0; i < methods.Count; i++)
                    {
                        var method = methods[i];

                        if (method.Name == "ToString" ||
                            method.Name == "GetType" ||
                            method.Name == "Equals" ||
                            method.Name == "GetHashCode")
                            continue;

                        if (method.Name.ToLower().Contains(line))
                        {
                            str.Append($"/* {i} */ ");

                            if (method.IsPublic)
                                str.Append("public ");
                            else if (method.IsPrivate)
                                str.Append("private ");

                            if (method.IsStatic)
                                str.Append("static ");

                            str.Append($"{method.ReturnType.Name} {method.Name}(");

                            var p = method.GetParameters();
                            for (int x = 0; x < p.Length; x++)
                            {
                                var param = p[x];

                                if (x != 0)
                                    str.Append(", ");

                                str.Append($"{param.ParameterType.Name} {param.Name}");

                                if (param.HasDefaultValue)
                                {
                                    string value = param.DefaultValue != null ? param.DefaultValue.ToString() : "null";
                                    str.Append($" = {value}");
                                }
                            }

                            str.AppendLine(");");
                        }
                    }

                    if (str.Length == 0)
                    {
                        arg.Channel.SendMessageAsync("Nothing found.");
                        return Task.CompletedTask;
                    }

                    string title = $"Found {str.ToString().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length} functions in API. ";

                    if (line.Length >= 1)
                        title += $"Selected only with ``{line}`` in the name.";

                    if (str.Length >= 1850)
                    {
                        FileInfo file = new FileInfo("api.txt");

                        using (StreamWriter temp = new StreamWriter(file.FullName))
                            temp.Write(str.ToString());

                        arg.Channel.SendFileAsync(file.FullName, title).GetAwaiter().GetResult();

                        file.Delete();
                    }
                    else
                    {
                        arg.Channel.SendMessageAsync(title + Environment.NewLine + "```csharp" + Environment.NewLine + str.ToString() + "```");
                    }

                    return Task.CompletedTask;
                }
                else if (arg.Content.ToLower().StartsWith("ai.help"))
                {
                    string[] args = arg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // if(args.Length == 1)

                    if (args.Length == 1)
                    {
                        str.AppendLine("AI by Dz3n#8831");
                        str.AppendLine();
                        str.AppendLine("Available commands:");
                        str.AppendLine("**ai.invite** - invite this bot to your server");
                        str.AppendLine("**ai.wordcomb** - generates word combination with one random word");
                        str.AppendLine("**ai.wordcomb [word]** - generates word combination with that word");
                        str.AppendLine("**ai.wordcomb [word] [count]** - generates phrase with that word");
                        str.AppendLine("**ai.randwordcomb [count]** - generates word combinations");
                        str.AppendLine("**ai.15words, ai.15phrases** - TOPs");
                        // str.AppendLine("**ai.10props [property]** - TOP 10 of choosen property");
                        str.AppendLine("**ai.stop** - do not disturb");
                        str.AppendLine("***any other sentence*** - AI will learn it");
                        str.AppendLine();
                        str.AppendLine("**ai.word [word]** - display or edit word properties");
                        str.AppendLine("**ai.denysim [word] [word]** - set words can't be similar");
                        str.AppendLine("**ai.dosim [initial] [word]** - set initial and similar words");
                        str.AppendLine("**ai.delsim [word] [word]** - removes initial or similarity from words");
                        str.AppendLine("**ai.wordnoprop** - displays 15 most used words without properties");
                        str.AppendLine();
                        str.AppendLine("**ai.stats** - for nerds");
                        str.AppendLine("**ai.interact** - change bot settings on current server");
                        str.AppendLine("**ai.api [text]** - search for API functions");
                        str.AppendLine("**ai.api** - dispaly all API functions");
                        str.AppendLine();
                        str.AppendLine("AI and Discord Bot are in **alpha** development stage, so they are unstable and updating very often. If you have any questions, ask Dz3n#8831.");
                    }

                    arg.Channel.SendMessageAsync(str.ToString());

                    return Task.CompletedTask;
                }
            }

            AI?.Learn(arg.Content, GetOwner(arg.Author, arg.Channel));

            return Task.CompletedTask;
        }

        public static string GetOwner(SocketUser user, ISocketMessageChannel channel)
        {
            StringBuilder owner = new StringBuilder();

            owner.Append(Token2);
            owner.Append(" " + user.Id);

            if (channel is SocketGuildChannel)
            {
                var gc = channel as SocketGuildChannel;
                owner.Append(" " + gc.Guild.Id);
            }
            else
                owner.Append(" 0");

            owner.Append(" " + channel.Id);

            return owner.ToString();
        }

        private static int GetSourceLines()
        {
            int total = 0;
            string[] folders = { "D:\\vsprojects\\MinPhraseAI", "D:\\vsprojects\\discordphraseai" };
            List<string> source = new List<string>();

            foreach (var folder in folders)
                source.AddRange(Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));

            foreach (var file in source)
            {
                try
                {
                    using (StreamReader r = new StreamReader(file))
                    {
                        string[] lines = r.ReadToEnd().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        total += lines.Length;
                    }
                }
                catch { }
            }

            return total;
        }

        private static void InteractSelector_UserMadeChoice(DiscordSelector selector, string reaction)
        {
            selector.Message.DeleteAsync();

            if (reaction == null) return;

            var channel = selector.Channel as SocketGuildChannel;
            var server = channel.Guild;

            if (reaction == DiscordSelector.DefaultYes)
            {
                if (!Config.InteractOnServers.Contains(server.Id))
                    Config.InteractOnServers.Add(server.Id);

                selector.Channel.SendMessageAsync(
                    $"<@{selector.User.Id}>, bot will interact with users on this server.");
            }
            else if (reaction == DiscordSelector.DefaultNo)
            {
                if (Config.InteractOnServers.Contains(server.Id))
                    Config.InteractOnServers.Remove(server.Id);

                selector.Channel.SendMessageAsync(
                    $"<@{selector.User.Id}>, bot will **not** interact with users on this server.");
            }
        }

        private static void LearnSelector_UserMadeChoice(DiscordSelector selector, string reaction)
        {
            selector.Message.DeleteAsync();

            if (reaction == null) return;

            var channel = selector.Channel as SocketGuildChannel;
            var server = channel.Guild;

            if (reaction == DiscordSelector.DefaultYes)
            {
                if (!Config.LearnOnServers.Contains(server.Id))
                    Config.LearnOnServers.Add(server.Id);

                selector.Channel.SendMessageAsync(
                    $"<@{selector.User.Id}>, bot will learn words from users from this server.");
            }
            else if (reaction == DiscordSelector.DefaultNo)
            {
                if (Config.LearnOnServers.Contains(server.Id))
                    Config.LearnOnServers.Remove(server.Id);

                selector.Channel.SendMessageAsync(
                    $"<@{selector.User.Id}>, bot will **not** learn words from users from this server");
            }
        }

        private static System.Threading.Tasks.Task Client_Ready()
        {
            ModuleServerStatus.StartThread(AI, client);
            return Task.CompletedTask;
        }
    }
}
