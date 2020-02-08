using System;
using System.Collections.Generic;
using System.Text;

namespace MinPhraseAI
{
    public class Phrase : BasicDBEntry
    {
        public Phrase() { }

        public Phrase(int id, string value, string owner = "")
        {
            ID = id;
            Value = value;
            Owner = owner;
        }

        public Phrase(string value, string owner = "")
        {
            Value = value;
            Owner = owner;
        }

        private static string NormalizePhraze(string input)
        {
            input = input.ToLower().Trim();
            
            foreach (var b in StuffClass.BadChars)
                input = input.Replace(b.ToString(), "");

            return input;
        }
        
        public static Phrase GetFromText(string value)
        {
            if (value == null) return null;

            string phrase = NormalizePhraze(value);

            if (phrase.Length < 1) return null;

            return new Phrase(phrase);
        }

        public override string ToString()
           => Value;
    }
}
