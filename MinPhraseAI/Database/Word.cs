using System;
using System.Collections.Generic;
using System.Text;

namespace MinPhraseAI
{
    public class Word : BasicDBEntry
    {
        public Word(int id, string value)
        {
            ID = id;
            Value = value;
        }

        public Word(string value)
        {
            Value = value;
        }

        public Word() { }

        public static bool NormalChars(string str)
        {
            foreach (var c in str)
                if (char.IsLetter(c))
                    return true;

            return false;
        }

        public static Word GetFromText(string value)
        {
            if (value == null) return null;

            value = value.Trim().ToLower();

            if (value.StartsWith("http://") || value.StartsWith("https://")) return null;

            if (!NormalChars(value)) return null;

            foreach (var b in StuffClass.BadChars)
                value = value.Replace(b.ToString(), "");

            return new Word(value);
        }

        public override string ToString()
            => Value;
    }
}
