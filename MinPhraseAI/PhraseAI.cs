using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MinPhraseAI
{
    public class PhraseAI
    {
        public StuffClass Stuff;
        public GeneratorClass Generate;
        public SettingsClass Settings;

        public delegate void LogLineHandler(string text);
        public event LogLineHandler LogLine;

        public delegate void BotMisunderstandHandler(ImportantThings.MisunderstandType type, string[] owners, string last_owner, params BasicDBEntry[] something);
        public event BotMisunderstandHandler BotMisunderstand;

        private string ConnectionString;
        // public MySqlConnection DatabaseMain;

        /// <summary>
        /// Create PhraseAI instance and connect to MySQL database
        /// </summary>
        public PhraseAI(string server, string database, string user, string password, string charset = "utf8")
        {
            ConnectionString = $"server={server};user id={user};password={password};persistsecurityinfo=True;database={database};CharSet={charset}";

            Stuff = new StuffClass(this);
            Generate = new GeneratorClass(this);
            Settings = new SettingsClass(this);
        }

        public Version GetVersion() => System.Reflection.Assembly.GetAssembly(GetType()).GetName().Version;

        public MySqlConnection GetTemp() => GetTemporaryDatabaseEx(0);

        public MySqlConnection GetTemporaryDatabaseEx(int timeout = 20)
        {
            MySqlConnection db = new MySqlConnection(ConnectionString);
            db.Open();

            if (timeout >= 1)
            {
                new Thread(() =>
                {
                    Thread.Sleep(TimeSpan.FromSeconds(timeout));
                    db.Close();
                }).Start();
            }

            return db;
        }

        /// <summary>
        /// Tell AI to learn this phrase (async)
        /// </summary>
        /// <param name="phraseo">Phrase to learn</param>
        public void Learn(string phrases, string owner = "")
        {
            new Thread(() =>
            {
                string[] phrasess = phrases.Split(new string[] { ".", "?", "!", "\"", "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var phrase in phrasess)
                    LearnEx(phrase, owner);
            }).Start();
        }

        private void LearnEx(string phraseo, string owner = "")
        {
            Phrase phrase = Phrase.GetFromText(phraseo);

            if (phrase == null)
                return;

            phrase.Owner = owner;

            string[] words = phrase.Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= 0)
                return;

            var idx = PhraseIndex(phrase.Value);

            if (idx != -1)
            {
                // LogLine?.Invoke("Known phrase: " + idx);
                IncreasePhraseUsageCount(idx);
                phrase.ID = idx;
            }
            else
            {
                if (words.Length > 1)
                {
                    var db = Command($"INSERT INTO phrases (value) VALUES('{phrase.Value.Protect()}');", null, false);
                    phrase.ID = LastInsertId(db);
                    // LogLine?.Invoke($"New phrase: " + phrase.ID)
                }
            }

            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];

                var wordid = LearnWord(word);

                AssociateWordAndPhrase(wordid, phrase.ID, i);
                AddWordOwner(wordid, owner);
                CheckWordSimilarityForMisunderstand(wordid);
            }

            AddPhraseOwner(phrase.ID, owner);
        }

        // fix me: make as RandomWord
        public Phrase RandomPhrase() => GetPhrase(StuffClass.random.Next(0, (int)MaxPhrase()));
        public Word RandomWord()
        {
            Word word;

            while (true)
            {
                word = GetWord(StuffClass.random.Next(0, (int)MaxWord()));

                if (word == null) continue;

                if (Settings.GetBool("use_only_verified"))
                    if (CountOfProperties(word.ID) >= 1)
                        continue;

                return word;
            }
        }

        public List<Word> GetAvailableWords(string additional = ";")
        {
            var result = new List<Word>();
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words{additional}", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Word()
                        {
                            ID = (int)reader["id"],
                            UsageCount = (int)reader["usage_count"],
                            Value = (string)reader["value"]
                        });
                    }
                }
            }

            return result;
        }

        public void AddPhraseOwner(long phrase, string owner)
        {
            if (!PhraseHasOwner(phrase, owner) && owner.Length >= 1)
                Command($"INSERT INTO phrases_owners (phrase_id,value) VALUES('{phrase}','{owner.Protect()}');");
        }

        public void AddWordOwner(long word, string owner)
        {
            if (!WordHasOwner(word, owner) && owner.Length >= 1)
                Command($"INSERT INTO words_owners (word_id,value) VALUES('{word}','{owner.Protect()}');");
        }

        public bool PhraseHasOwner(long word, string owner)
            => Scalar($"SELECT COUNT(*) FROM phrases_owners WHERE phrase_id = '{word}' AND value = '{owner}';") >= 1;

        public bool WordHasOwner(long word, string owner)
            => Scalar($"SELECT COUNT(*) FROM words_owners WHERE word_id = '{word}' AND value = '{owner}';") >= 1;

        public List<string> GetPhraseOwners(long phrase)
        {
            var result = new List<string>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM phrases_owners WHERE phrase_id = '{phrase}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add((string)reader["value"]);
                    }
                }
            }

            return result;
        }

        public List<string> GetWordOwners(long word)
        {
            var result = new List<string>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words_owners WHERE word_id = '{word}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add((string)reader["value"]);
                    }
                }
            }

            return result;
        }

        public List<Word> GetQuestionsWords()
        {
            var result = new List<Word>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words WHERE value LIKE '%?';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Word()
                        {
                            ID = (int)reader["id"],
                            UsageCount = (int)reader["usage_count"],
                            Value = (string)reader["value"]
                        });
                    }
                }
            }

            return result;
        }

        public List<Phrase> GetAvailablePhrases(string additional = ";")
        {
            var result = new List<Phrase>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM phrases{additional}", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Phrase()
                        {
                            ID = (int)reader["id"],
                            UsageCount = (int)reader["usage_count"],
                            Value = (string)reader["value"]
                        });
                    }
                }
            }

            return result;
        }

        public Word GetWord(long id)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words WHERE id = '{id}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Word result = new Word((int)reader["id"], (string)reader["value"])
                        {
                            UsageCount = (int)reader["usage_count"]
                        };

                        return result;
                    }
                }
            }

            return null;
        }

        public Phrase GetPhrase(long id)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM phrases WHERE id = '{id}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Phrase result = new Phrase((int)reader["id"], (string)reader["value"])
                        {
                            UsageCount = (int)reader["usage_count"]
                        };

                        return result;
                    }
                }
            }

            return null;
        }

        public void AssociateWordAndPhrase(long WordID, long PhraseID, int position = -1)
        {
            if (PhraseID > 0 && WordID > 0 && !IsItAssociated(WordID, PhraseID))
            {
                Command($"INSERT INTO words_relationship (word_id,phrase_id,position) VALUES('{WordID}', '{PhraseID}', '{position}');");
                // LogLine?.Invoke($"Word {WordID} + Phrase {PhraseID} (pos: {position})");
            }
        }

        public long[] GetAssociatedWords(long phrase)
        {
            List<long> result = new List<long>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand("SELECT * FROM words_relationship;", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if ((int)reader["phrase_id"] == phrase)
                            result.Add((int)reader["word_id"]);
                    }
                }
            }

            return result.ToArray();
        }

        public long[] GetAssociatedPhrases(long word)
        {
            List<long> result = new List<long>();

            using (var con = GetTemp())
            {
                var command = new MySqlCommand("SELECT * FROM words_relationship;", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if ((int)reader["word_id"] == word)
                            result.Add((int)reader["phrase_id"]);
                    }
                }
            }

            return result.ToArray();
        }

        public bool IsItAssociated(long word, long phrase, MySqlConnection database = null)
            => Scalar($"SELECT COUNT(*) FROM words_relationship WHERE word_id = '{word}' AND phrase_id = '{phrase}';", database) >= 1;

        public bool WordExists(string value) => WordIndex(value) != -1;

        public bool PhraseExists(string value) => PhraseIndex(value) != -1;

        public long PhraseIndex(string value)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM phrases WHERE value = '{value.Protect()}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return (int)reader["id"];
                    }
                }
            }

            return -1;
        }

        public int WordIndex(string value)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words WHERE value = '{value.Protect()}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return (int)reader["id"];
                    }
                }
            }

            return -1;
        }

        public void DeletePhrase(long id) => Command($"DELETE FROM phrases WHERE id = '{id}';");

        public void DeleteWord(long id) => Command($"DELETE FROM words WHERE id = '{id}';");

        public int GetWordPosition(long word, long phrase)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand(
                    $"SELECT * FROM words_relationship WHERE phrase_id = '{phrase}' AND word_id = '{word}';",
                    con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return (int)reader["position"];
                    }
                }
            }

            return -1;
        }

        public int GetWordUsageCount(long id)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT usage_count FROM words WHERE id = '{id}';",
                    con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // LogLine?.Invoke($"Word {id}, get UsageCount = {(int)reader["usage_count"]}");
                        return (int)reader["usage_count"];
                    }
                }
            }

            return -1;
        }

        public void IncreaseWordUsageCount(long id)
        {
            var current = GetWordUsageCount(id);

            if (current == -1)
                return;

            current++;

            Command($"UPDATE words SET usage_count = '{current}' WHERE id = '{id}';");

            CheckWordPropertiesForMisunderstand(id, current);
        }

        public int GetPhraseUsageCount(long id)
        {
            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT usage_count FROM phrases WHERE id = '{id}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // LogLine?.Invoke($"Phrase {id}, get UsageCount = {(int)reader["usage_count"]}");
                        return (int)reader["usage_count"];
                    }
                }
            }
            return -1;
        }

        public void IncreasePhraseUsageCount(long id)
        {
            var current = GetPhraseUsageCount(id);

            if (current == -1)
                return;

            current++;

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"UPDATE phrases SET usage_count = '{current}' WHERE id = '{id}';", con);
                command.ExecuteNonQuery();
            }
        }

        public void RemoveInitialForWord(long word)
        {
            var initial = GetWordInitial(word);
            Command($"DELETE FROM words_similar WHERE word_id = '{word}' AND initial = '{initial}';");
        }

        public void RemoveSimilar(long word, long b)
        {
            Command($"DELETE FROM words_similar WHERE (word_id = '{word}' AND initial = '{b}') OR (word_id = '{b}' AND initial = '{word}';");
        }

        public bool AddSimilar(long initial, long word)
        {
            if (WordHasInitial(initial)) initial = GetWordInitial(initial);
            if (WordHasInitial(word)) word = GetWordInitial(word);

            if (word == initial)
                return false;

            if (!WordsAreSimilar(initial, word))
            {
                LogLine?.Invoke($"Initial word '{GetWord(initial).Value}' for word '{GetWord(word).Value}'.");
                Command($"INSERT INTO words_similar (initial,word_id) VALUES('{initial}','{word}')");
                return true;
            }

            return false;
        }

        public bool WordsAreSimilar(long worda, long wordb)
            => Scalar($"SELECT COUNT(*) FROM words_similar WHERE word_id = '{worda}' OR initial = '{worda}' OR word_id = '{wordb}' OR initial = '{wordb}';") >= 1;

        public bool WordHasInitial(long word)
            => Scalar($"SELECT COUNT(*) FROM words_similar WHERE word_id = '{word}';") >= 1;

        public long GetWordInitial(long word)
        {
            using (var con = GetTemp())
            {
                MySqlCommand command = new MySqlCommand($"SELECT * FROM words_similar WHERE word_id = '{word}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        return (int)reader["initial"];
                    }
                }
            }

            return -1;
        }

        public bool WordHasSimilar(long word)
            => CountOfSimilar(word) >= 1;

        public long CountOfSimilar(long word)
            => Scalar($"SELECT COUNT(*) FROM words_similar WHERE word_id = '{word}' OR initial = '{word}';");

        public bool WordSimilarDenied(long worda, long wordb)
            => Scalar($"SELECT COUNT(*) FROM words_similar_deny WHERE (word_a = '{worda}' AND word_b = '{wordb}') OR (word_b = '{worda}' AND word_a = '{wordb}');") >= 1;

        public void WordSimilarDeny(long worda, long wordb)
        {
            if (!WordSimilarDenied(worda, wordb))
            {
                LogLine?.Invoke($"Deny similar '{WordValue(worda)}' and '{WordValue(wordb)}'");
                Command($"INSERT INTO words_similar_deny (word_a,word_b) VALUES('{worda}','{wordb}');");
            }
        }

        public void WordSimilarAllow(long worda, long wordb)
        {
            if (!WordSimilarDenied(worda, wordb))
            {
                LogLine?.Invoke($"Allow similar '{WordValue(worda)}' and '{WordValue(wordb)}'");
                Command($"INSERT INTO words_similar_deny (word_a,word_b) VALUES('{worda}','{wordb}');");
            }
        }

        public long[] GetSimilar(long word, bool IncludeThisWord = false)
        {
            List<long> result = new List<long>();

            using (var con = GetTemp())
            {
                MySqlCommand command = new MySqlCommand($"SELECT * FROM words_similar WHERE word_id = '{word}' OR initial = '{word}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if ((int)reader["word_id"] != word)
                            result.Add((int)reader["word_id"]);
                    }
                }
            }

            if (IncludeThisWord)
                result.Add(word);

            return result.ToArray();
        }
        public long CountOfProperties(long word)
           => Scalar($"SELECT COUNT(*) FROM words_props WHERE word_id = '{word}';");

        private void CheckWordPropertiesForMisunderstand(long word, int current)
        {
            var cnt = CountOfProperties(word);

            if ((cnt == 0 && (current == 25 || current == 50 || current == 75)) ||
                (cnt == 1 && (current >= 100 && (current % 100) == 0)))
            {
                var owners = GetWordOwners(word);
                var last = owners[owners.Count - 1];

                BotMisunderstand?.Invoke(ImportantThings.MisunderstandType.WordProperties,
                    owners.ToArray(),
                    last,
                    GetWord(word));
            }
        }

        /// <summary>
        /// Tell AI to learn this word
        /// </summary>
        /// <param name="word"></param>
        private long LearnWord(string wordo)
        {
            Word word = Word.GetFromText(wordo);

            if (word == null || word.Value.Length <= 0 || string.IsNullOrWhiteSpace(word.Value))
                return -1;

            var idx = WordIndex(word.Value);

            if (idx != -1)
            {
                // LogLine?.Invoke($"Known word: {idx}");
                word.ID = idx;
                IncreaseWordUsageCount(idx);
            }
            else
            {
                var db = Command($"INSERT INTO words (value) VALUES('{word.Value.Protect()}');", null, false);
                word.ID = LastInsertId(db);
                db.Dispose();
                // LogLine?.Invoke($"New word: " + word.ID);
            }

            return word.ID;
        }

        private void CheckWordSimilarityForMisunderstand(long word)
        {
            var currentw = GetWord(word);

            if (currentw == null)
                return;

            var current = currentw.Value;

            if (currentw.UsageCount < 30 || WordHasSimilar(word))
                return;

            var sim = GetSimilarWord(word);

            if (sim != -1)
            {

                var owners = GetWordOwners(word);

                if (owners.Count >= 1)
                {
                    var last = owners[owners.Count - 1];

                    BotMisunderstand?.Invoke(ImportantThings.MisunderstandType.WordSimilarity,
                        owners.ToArray(),
                        last,
                        GetWord(word), GetWord(sim));
                }
            }
        }

        public long GetSimilarWord(long word)
        {
            var current = WordValue(word);
            var min = double.Parse(Settings.GetString("min_word_similarity"));

            using (var con = GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM words;", con);
                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = (int)reader["id"];
                        var value = reader["value"] as string;

                        if (id != word)
                        {
                            var similarity = SynonymsClass.CalculateSimilarity(value, current);

                            if (similarity >= min && !WordSimilarDenied(word, id))
                            {
                                return id;
                            }
                        }
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Removes all phrases and words from database
        /// </summary>
        public void DeleteEverything(bool confirm)
        {
            if (confirm)
            {
                foreach (var table in ImportantThings.TablesToWipe)
                {
                    LogLine?.Invoke("Wiping table: " + table);
                    Command($"TRUNCATE TABLE {table};");
                }

                Settings.Set("wipe_date", DateTime.Now.ToString());
                LogLine?.Invoke("Wiping completed.");
            }
        }

        public void DeleteOwners(bool confirm)
        {
            if (confirm)
            {
                Command($"TRUNCATE TABLE phrases_owners;");
                Command($"TRUNCATE TABLE words_owners;");

                LogLine?.Invoke("Wiping completed.");
            }
        }

        public void AddWordProp(long word, ImportantThings.WordProp prop)
        {
            if (WordHasProp(word, prop)) return;

            Command($"INSERT INTO words_props (word_id,value) VALUES('{word}','{(int)prop}');");
        }

        public void RemoveWordProp(long word, ImportantThings.WordProp prop)
        {
            if (WordHasInitial(word) && WordHasProp(GetWordInitial(word), prop))
            {
                LogLine?.Invoke($"Remove props for {WordValue(word)} + for initial {WordValue(GetWordInitial(word))}");
                RemoveWordProp(GetWordInitial(word), prop);
            }

            if (!WordHasProp(word, prop)) return;

            Command($"DELETE FROM words_props WHERE word_id = '{word}' AND value = '{(int)prop}';");
        }

        public void ClearWordProps(long word)
        {
            Command($"DELETE FROM words_props WHERE word_id = '{word}';");
        }

        public string WordValue(long word) => GetWord(word).Value;

        public List<ImportantThings.WordProp> GetWordProps(long word)
        {
            List<ImportantThings.WordProp> b = new List<ImportantThings.WordProp>();

            if (WordHasInitial(word))
            {
                LogLine?.Invoke($"Props for {WordValue(word)} but + initial {WordValue(GetWordInitial(word))}");
                b.AddRange(GetWordProps(GetWordInitial(word)));
            }

            using (var connection = GetTemp())
            {
                MySqlCommand command = new MySqlCommand($"SELECT * FROM words_props WHERE word_id = '{word}';", connection);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int value = (int)reader["value"];
                        b.Add((ImportantThings.WordProp)value);
                    }
                }
            }

            return b;
        }

        public bool WordHasProp(long word, ImportantThings.WordProp prop)
        {
            if (WordHasInitial(word))
            {
                LogLine?.Invoke($"Has props for {WordValue(word)} but returning for initial {WordValue(GetWordInitial(word))}");
                var init = WordHasProp(GetWordInitial(word), prop);

                if (init)
                    return true;
            }

            return Scalar($"SELECT COUNT(*) FROM words_props WHERE word_id = '{word}' AND value = '{(int)prop}';") >= 1;
        }

        public long MaxPhrase() => Scalar("SELECT MAX(id) FROM phrases;");

        public long MaxWord() => Scalar("SELECT MAX(id) FROM words;");

        public MySqlConnection Command(string cmd, MySqlConnection database = null, bool CloseIfNull = true)
        {
            MySqlConnection db;

            if (database == null) db = GetTemp();
            else db = database;

            new MySqlCommand(cmd, db).ExecuteNonQuery();

            if (database == null && CloseIfNull) db.Dispose();

            return db;
        }

        public long Scalar(string cmd, MySqlConnection database = null)
        {
            MySqlConnection db;

            if (database == null) db = GetTemp();
            else db = database;

            var command = new MySqlCommand(cmd, db);
            var result = command.ExecuteScalar();

            if (database == null) db.Dispose();

            if (result is int)
                return (int)result;
            else if (result is long)
                return (long)result;
            else
                return -1;
        }

        public long CountOf(string table, MySqlConnection db = null)
            => Scalar($"SELECT COUNT(*) FROM {table};", db);

        private Int64 LastInsertId(MySqlConnection source) => (Int64)new MySqlCommand("SELECT LAST_INSERT_ID();", source).ExecuteScalar();
    }
}
