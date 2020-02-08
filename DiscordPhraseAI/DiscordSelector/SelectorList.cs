using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordPhraseAI
{
    public class SelectorList
    {
        public object Tag = null;

        public ISocketMessageChannel Channel;
        public SocketUser User;
        public DiscordSelector Selector;
        public RestUserMessage Message => Selector.Message;
        public List<SelectorItem> Items = new List<SelectorItem>();

        public delegate void UserMadeChoiceHandler(SelectorList selector, SelectorItem item);
        public event UserMadeChoiceHandler UserMadeChoice;

        private int CurrentStartPoint = 0;
        private const int MaxDisplay = 10;
        
        private string Content = "";

        public SelectorList(string content, ISocketMessageChannel channel, SocketUser user)
        {
            Content = content;
            Channel = channel;
            User = user;
        }

        private void Selector_UserMadeChoice(DiscordSelector selector, string reaction)
        {
            Message.DeleteAsync();

            if(reaction == DiscordSelector.DefaultLeft) // go back
            {
                if (CurrentStartPoint >= MaxDisplay)
                {
                    CurrentStartPoint -= MaxDisplay;
                    Send();
                    return;
                }
            }
            else if(reaction == DiscordSelector.DefaultRight) // go next
            {
                if (CurrentStartPoint + MaxDisplay <= Items.Count)
                {
                    CurrentStartPoint += MaxDisplay;
                    Send();
                    return;
                }
            }
            else if (DiscordSelector.DefaultNumber.Contains(reaction)) // selected
            {
                var idx = DiscordSelector.DefaultNumber.IndexOf(reaction);
                var OnScreenItems = selector.Tag as List<SelectorItem>;
                var SelectedItem = OnScreenItems[idx];

                UserMadeChoice?.Invoke(this, SelectedItem);
                return;
            }
            else if (reaction == DiscordSelector.DefaultNo)
            {
                UserMadeChoice?.Invoke(this, null);
                return;
            }
        }

        public void Send()
        {
            List<SelectorItem> OnScreenItems = new List<SelectorItem>();
            StringBuilder b = new StringBuilder();
            
            for (int i = CurrentStartPoint; i < Items.Count; i++)
            {
                if (OnScreenItems.Count >= MaxDisplay) break;
                OnScreenItems.Add(Items[i]);
            }
            
            //b.Append("```");

            for (int i = 0; i < OnScreenItems.Count; i++)
            {
                b.AppendLine($"{DiscordSelector.DefaultNumber[i]} - {OnScreenItems[i].Name}");
            }

            // b.Append("```");

            b.Append($"Items from {CurrentStartPoint} to {OnScreenItems.Count - 1} are shown. Use {DiscordSelector.DefaultLeft} {DiscordSelector.DefaultRight} to scroll.");

            List<string> Reactions = new List<string>();

            // display left button
            if (CurrentStartPoint >= MaxDisplay)
                Reactions.Add(DiscordSelector.DefaultLeft);

            // display numbers
            for (int i = 0; i < OnScreenItems.Count; i++) // should be not more than MaxDisplay
                Reactions.Add(DiscordSelector.DefaultNumber[i]);

            // display right button
            if (CurrentStartPoint + MaxDisplay <= Items.Count)
                Reactions.Add(DiscordSelector.DefaultRight);

            // display stop button
            Reactions.Add(DiscordSelector.DefaultNo);

            Selector = new DiscordSelector(Content + Environment.NewLine + b.ToString(), Reactions.ToArray(), Channel, User, 0);
            Selector.Tag = OnScreenItems;
            Selector.UserMadeChoice += Selector_UserMadeChoice;
            Selector.Send();
        }
    }

    public class SelectorItem
    {
        public string Name;
        public object Value;

        public SelectorItem(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}
