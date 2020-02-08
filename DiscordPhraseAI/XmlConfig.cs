using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace DiscordPhraseAI
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
        public List<ulong> BannedServers = new List<ulong>();
        public List<ulong> BannedUsers = new List<ulong>();
        public List<ulong> DoNotDisturb = new List<ulong>();

        public List<ulong> AdminUsers = new List<ulong>();

        public List<ulong> LearnOnServers = new List<ulong>();
        public List<ulong> InteractOnServers = new List<ulong>();

        public ModuleServerStatusClass ServerStatusThing = new ModuleServerStatusClass();

        public int ChannelSpamTime = 15;

        public SecretClass KeepThisBotTokenInSecretOk = new SecretClass("NTQzMTAzMTMwMDkxNTIwMDEx.Dz3tsw.TA5SQjasWvlzXaNGWQdU6Bf50qI");

        public string BotToken { get => KeepThisBotTokenInSecretOk.BotToken; }
    }

    [Serializable]
    public class ModuleServerStatusClass
    {
        public ulong Guild = 566289126689865769;
        public ulong VoiceChannel1 = 567734902234021890;
        public ulong VoiceChannel2 = 567735106752610315;
        public ulong VoiceChannel3 = 567738570547396623;
        public ulong VoiceChannel4 = 567738654982668288;
        public ulong VoiceChannel5 = 567771925850488851;
    }

    [Serializable]
    public class SecretClass
    {
        public string BotToken = "";
        public SecretClass() { }
        public SecretClass(string token) { BotToken = token; }
    }
}
