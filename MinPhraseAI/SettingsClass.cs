using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinPhraseAI
{
    public class SettingsClass
    {
        private PhraseAI ai;

        public SettingsClass(PhraseAI AI) => ai = AI;

        public object GetValueObject(string name)
        {
            using (var con = ai.GetTemp())
            {
                var command = new MySqlCommand($"SELECT * FROM settings WHERE name = '{name}';", con);

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        object value = reader["value"];
                        object value_str = reader["value_str"];

                        if (value is int)
                            return value;
                        else if (value_str is string)
                            return value_str;
                        else
                            return null;
                    }
                }
            }

            return null;
        }

        public void Set(string name, object value)
        {
            using (var con = ai.GetTemp())
            {
                new MySqlCommand($"UPDATE settings SET value = NULL WHERE name = '{name}';", con).ExecuteNonQuery();
                new MySqlCommand($"UPDATE settings SET value_str = NULL WHERE name = '{name}';", con).ExecuteNonQuery();

                if (value == null) return;

                if (value is string)
                    new MySqlCommand($"UPDATE settings SET value_str = '{(string)value}' WHERE name = '{name}';", con).ExecuteNonQuery();
                else
                    new MySqlCommand($"UPDATE settings SET value = '{value}' WHERE name = '{name}';", con).ExecuteNonQuery();
            }
        }

        public bool GetBool(string name)
        {
            var a = GetValue(name);

            if (a == 1)
                return true;
            else
                return false;
        }

        public int GetValue(string name)
        {
            var a = GetValueObject(name);

            if (a is int)
                return (int)a;
            else
                return -1;
        }

        public string GetString(string name) => GetValueObject(name) as string;
    }
}
