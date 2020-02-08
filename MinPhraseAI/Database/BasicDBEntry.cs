using System;
using System.Collections.Generic;
using System.Text;

namespace MinPhraseAI
{
    public class BasicDBEntry
    {
        public long ID = -1;
        public string Value;
        public int UsageCount = 0;
        public string Owner;

        public override string ToString()
             => Value;
    }
}
