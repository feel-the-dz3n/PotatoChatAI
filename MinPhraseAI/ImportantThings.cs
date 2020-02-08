using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinPhraseAI
{
    public class ImportantThings
    {
        public const int MisWordPropsAtUsageCount = 20;

        public static string[] TablesToWipe =
        {
            "words", "phrases", "words_relationship", "words_props", "phrases_owners", "words_owners", "words_similar_deny", "words_similar"
        };

        public enum WordProp
        {
            Can_i_tExplain,
            GoodMood,
            BadMood,
            ShouldBeRemoved,
            ConversationPart,
            Appeal,
            Swearing,
            Answer,
            Reply,
            Reaction,
            GrammarArticle,
            GivenName,
        }

        public enum MisunderstandType
        {
            WordProperties,
            WordSimilarity
        }

        public static Dictionary<int, string> GetFriendlyNames(Type enumType)
        {
            Dictionary<int, string> r = new Dictionary<int, string>();

            var names = Enum.GetNames(enumType);

            for (int i = 0; i < names.Length; i++)
            {
                r.Add(i, GetFriendlyName(names[i]));
            }

            return r;
        }
        
        public static string GetFriendlyName(string source)
        {
            StringBuilder a = new StringBuilder();

            bool IgnoreUpper = false;

            source = source.Replace("_i_", "'");

            for (int i = 0; i < source.Length; i++)
            {
                var c = source[i];

                if (!IgnoreUpper && i != 0 && char.IsUpper(c))
                    a.Append(' ');

                if (c == '_')
                {
                    c = ' ';

                    if (i == 0)
                    {
                        IgnoreUpper = true;
                        continue;
                    }
                }

                a.Append(c);
            }

            return a.ToString();
        }
    }
}
