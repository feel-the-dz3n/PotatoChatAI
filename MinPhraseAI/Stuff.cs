using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinPhraseAI
{
    public class StuffClass
    {
        public static Random random = new Random();

        public static char[] BadChars = { '!', '.', ',', ';', ':', '`', '*', '_', '~', '|', '<', '>', '(', ')', '{', '}', '[', ']', '\\', '/' };

        private PhraseAI ai;

        public StuffClass(PhraseAI AI) => ai = AI;

        public List<Word> WordsWithoutProperties(int count = 10)
        {
            List<Word> result = new List<Word>();

            var words = ai.GetAvailableWords($" ORDER BY usage_count DESC;");

            for(int i = 0; i < words.Count; i++)
            {
                if (result.Count >= count) break;

                if (ai.CountOfProperties(words[i].ID) == 0)
                    result.Add(words[i]);
            }

            return result;
        }

        public Word[] TopWords(int count = 5)
            => ai.GetAvailableWords($" ORDER BY usage_count DESC LIMIT {count};").ToArray();
        
        public Phrase[] TopPhrases(int count = 5)
            => ai.GetAvailablePhrases($" ORDER BY usage_count DESC LIMIT {count};").ToArray();
    }
}
