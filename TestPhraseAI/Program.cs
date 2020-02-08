using MinPhraseAI;
using System;
using System.IO;

namespace TestPhraseAI
{
    class Program
    {
        static void Main(string[] args)
        {
            string database = "minphraseglobal";
            string host = "127.0.0.1";
            string user = "root";
            string pwd = "";

            Console.WriteLine($"Connecting {user}:{host} (db: {database})...");

            PhraseAI ai = new PhraseAI(host, database, user, pwd);
            ai.LogLine += Ai_LogLine;

            Console.WriteLine("Connected!");

            while (true)
            {
                Console.Write("Phrase to learn: ");

                var phrase = Console.ReadLine();

                if (phrase.StartsWith("IMPORT"))
                {
                    using (StreamReader a = new StreamReader("import.txt"))
                    {
                        string[] phrases = a.ReadToEnd().Split(new string[] { ".", "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var p in phrases)
                            ai.Learn(p);
                    }
                }
                else
                    ai.Learn(phrase);
            }
        }

        private static void Ai_LogLine(string text)
            => Console.WriteLine("[LOG]: " + text);
    }
}
