using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace DiscordServerStatusBot
{
    public static class ConfigStatic
    {
        public static DateTime StartTime = DateTime.Now;

        public const string ConfigFileName = "BotSettings.xml";
        public static XmlConfig Config = new XmlConfig();

        public static void InitSaveThread()
        {
            new Thread(() => 
            {
                while (true)
                {
                    Thread.Sleep(20000);
                    SaveSettings();
                }
            }).Start();
        }

        public static void LoadConfig()
        {
            if (!File.Exists(ConfigFileName))
            {
                // create settings
                SaveSettings();
                return;
            }

            XmlSerializer ser = new XmlSerializer(typeof(XmlConfig));

            using (StreamReader stream = new StreamReader(ConfigFileName))
            {
                Config = (XmlConfig)ser.Deserialize(stream);
            }
        }

        public static void SaveSettings()
        {
            XmlSerializer ser = new XmlSerializer(typeof(XmlConfig));

            using (StreamWriter stream = new StreamWriter(ConfigFileName))
            {
                ser.Serialize(stream, Config);
            }
        }
    }

    [Serializable]
    public class XmlConfig
    {
        public SecretClass KeepThisBotTokenInSecretOk = new SecretClass("NTQzMTAzMTMwMDkxNTIwMDEx.Dz3tsw.TA5SQjasWvlzXaNGWQdU6Bf50qI");

        public string BotToken { get => KeepThisBotTokenInSecretOk.BotToken; }
    }

    [Serializable]
    public class SecretClass
    {
        public string BotToken = "";
        public SecretClass() { }
        public SecretClass(string token) { BotToken = token; }
    }
}
