using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinPhraseAI
{
    public static class StringProtector
    {
        public static string Protect(this string input)
        {
            return input.Replace("'", "\\'");
        }
    }
}
