using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DiscordPhraseAI.ConfigStatic;

namespace DiscordPhraseAI
{
    public class DiscordSelector
    {
        public object Tag = null;

        public ISocketMessageChannel Channel;
        public SocketUser User;
        public RestUserMessage Message;

        public delegate void UserMadeChoiceHandler(DiscordSelector selector, string reaction);
        public event UserMadeChoiceHandler UserMadeChoice;

        private List<string> Reactions = new List<string>();
        public bool Done { get; private set; } = false;
        private string Content;

        public static string DefaultYes = "✅";
        public static string DefaultNo = "❎";
        public static string DefaultLeft = "⬅";
        public static string DefaultRight = "➡";
        public static string DefaultUp = "⬆";
        public static string DefaultDown = "⬇";
        public static string DefaultStop = "🛑";
        public static string DefaultKoKo = "🈁";
        public static string DefaultA = "🇦";
        public static string DefaultD = "🇩";
        public static string DefaultS = "🇸";
        public static string DefaultI = "🇮";
        public static string DefaultVS = "🆚";
        public static List<string> DefaultNumber = new List<string>()
        { "0⃣", "1⃣", "2⃣", "3⃣", "4⃣", "5⃣", "6⃣", "7⃣", "8⃣", "9⃣" };

        private int Timeout = 25;

        /// <summary>
        /// yes or no
        /// </summary>
        public DiscordSelector(string content, ISocketMessageChannel channel, SocketUser user, int timeout = 25)
        {
            Channel = channel;
            User = user;
            Content = content;
            Reactions.AddRange(new string[] { DefaultYes, DefaultNo });
            Timeout = timeout;
        }

        public DiscordSelector(string content, string[] reactions, ISocketMessageChannel channel, SocketUser user, int timeout = 25)
        {
            Channel = channel;
            User = user;
            Content = content;
            Reactions.AddRange(reactions);
            Timeout = timeout;
        }

        public void Send()
        {
            Message = Channel.SendMessageAsync(Content).GetAwaiter().GetResult();

            Program.client.ReactionAdded += Client_ReactionAdded;

            List<Emoji> emojis = new List<Emoji>();
            foreach (var reaction in Reactions)
                emojis.Add(new Emoji(reaction));

            Message.AddReactionsAsync(emojis.ToArray());

            if (Timeout > 0)
                new Thread(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(Timeout));

                    if(!Done)
                    {
                        Program.client.ReactionAdded -= Client_ReactionAdded;
                        Done = true;
                        UserMadeChoice?.Invoke(this, null);
                    }
                }).Start();
        }

        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (Done)
                return Task.CompletedTask;

            var user = arg3.User.Value;

            if (user != null && (user.IsBot || Config.BannedUsers.Contains(user.Id)))
                return Task.CompletedTask;

            if (User == null || (user != null && user.Id == User.Id))
            {
                var message = arg1.GetOrDownloadAsync().GetAwaiter().GetResult();
                var code = arg3.Emote.Name;

                if (message != null && message.Id == Message.Id)
                    if (Reactions.Contains(code))
                    {
                        if (User == null)
                            User = (SocketUser)user;

                        Program.client.ReactionAdded -= Client_ReactionAdded;
                        Done = true;
                        UserMadeChoice?.Invoke(this, code);
                    }
            }

            return Task.CompletedTask;
        }
    }
}
