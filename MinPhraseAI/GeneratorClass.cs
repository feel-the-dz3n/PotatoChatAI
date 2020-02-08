using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinPhraseAI
{
    public class GeneratorClass
    {
        private PhraseAI ai;

        public GeneratorClass(PhraseAI AI) => ai = AI;

        public string Sentence()
            => Sentence(ai.RandomWord().ID);

        public string Sentence(string word)
            => Sentence(ai.WordIndex(word));

        public string Sentence(long word)
        {
            return "test";
        }

        public string Question(string word)
        {
            var words = ai.GetQuestionsWords();

            if (words.Count <= 0)
                return "There are no questions in the database. Sorry.";

            var w = words[StuffClass.random.Next(0, words.Count)];

            var assoc = ai.GetAssociatedPhrases(w.ID);

            var phrase = assoc[StuffClass.random.Next(0, assoc.Length)];

            var assocw = ai.GetAssociatedWords(phrase);

            StringBuilder b = new StringBuilder();
            
            foreach(var wx in assocw)
            {
                var value = ai.GetWord(wx);

                if (value.Value.EndsWith("?"))
                    value.Value = word;

                b.Append($"{value} ");
            }

            return b.ToString();
        }

        /// <summary>
        /// Generates word combination from random word
        /// </summary>
        public string WordCombination()
            => WordCombination(ai.RandomWord().ID);

        /// <summary>
        /// Generates word combination related to one word
        /// </summary>
        public string WordCombination(string word)
            => WordCombination(ai.WordIndex(word));

        /// <summary>
        /// Generates word combination related to one word
        /// </summary>
        public string WordCombination(long word)
        {
            if (word == -1)
                return "Unknown word";

            // get all phrases related to this word
            var phrases = ai.GetAssociatedPhrases(word);

            if (phrases.Length == 0)
                return "No associations for W" + word;

            // now choose random phrase
            long phrase = phrases[StuffClass.random.Next(0, phrases.Length)];

            // get all words associated with this phrase
            var words = ai.GetAssociatedWords(phrase);

            // if there's no other words
            if (words.Length == 1 && words[0] == word)
                return "No associations for P" + phrase;

            // select random word but not the current one
            long id;
            while (true)
            {
                id = words[StuffClass.random.Next(0, words.Length)];
                if (id != word) break;
            }
            
            return ai.GetWord(word) + " " + ai.GetWord(id);
        } 
        
        /// <summary>
        /// Gets a random word related to the provided one
        /// </summary>
        public long GetRandomAssoc(long word)
        {
            if (word == -1)
                return -1;

            // get all phrases related to this word
            var phrases = ai.GetAssociatedPhrases(word);

            if (phrases.Length == 0)
                return -1;

            // now choose random phrase
            long phrase = phrases[StuffClass.random.Next(0, phrases.Length)];

            // get all words associated with this phrase
            var words = ai.GetAssociatedWords(phrase);

            // if there's no other words
            if (words.Length == 1 && words[0] == word)
                return -1;

            // select random word but not the current one
            long id;
            while (true)
            {
                id = words[StuffClass.random.Next(0, words.Length)];
                if (id != word) break;
            }

            return id;
        }

        /// <summary>
        /// Generates word combination of the specified length related to one word
        /// </summary>
        public string WordCombination(long word, int length = 2)
        {
            if (length < 2)
                return ai.WordValue(word);
            if (word == -1)
                return "Unknown word";

            string result = ai.WordValue(word);
            long tempWord = word;

            for (int i = 0; i < length; i++)
            {
                tempWord = GetRandomAssoc(tempWord);
                if (tempWord >= 0)
                    result += " " + ai.GetWord(tempWord);
                else
                    break;
            }

            return result;
        }
    }
}
