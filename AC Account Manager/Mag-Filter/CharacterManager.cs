﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MagFilter
{
    public class CharacterManager
    {
        private Dictionary<string, ServerCharacterListByAccount> _data = null;

        private CharacterManager(Dictionary<string, ServerCharacterListByAccount> dictionary)
        {
            _data = dictionary;
        }
        private CharacterManager()
        {
            _data = new Dictionary<string, ServerCharacterListByAccount>();
        }
        public IEnumerable<string> GetKeys()
        {
            return _data.Keys;
        }
        public ServerCharacterListByAccount GetCharacters(string serverName, string accountName)
        {
            string key = GetKey(server: serverName, accountName: accountName);
            if (this._data.ContainsKey(key))
            {
                return this._data[key];
            }
            else
            {
                return null;
            }
        }
        internal ServerCharacterListByAccount GetCharacters(string key)
        {
            return this._data[key];
        }
        private static string GetKey(string server, string accountName)
        {
            return string.Format("{0}-{1}", server, accountName);
        }

        public void WriteCharacters(string server, string zonename, List<Character> characters)
        {
            var launchInfo = (new LaunchControl()).GetLaunchInfo();
            if (!launchInfo.IsValid)
            {
                log.WriteLogMsg("LaunchInfo not valid");
                return;
            }
            log.WriteLogMsg("LaunchInfo valid");
            string key = GetKey(server: server, accountName: launchInfo.AccountName);
            var clist = new ServerCharacterListByAccount()
                {
                    ZoneId = zonename,
                    CharacterList = characters
                };
            this._data[key] = clist;
            string contents = JsonConvert.SerializeObject(_data, Formatting.Indented);
            string path = FileLocations.GetCharacterFilePath();
            using (var file = new StreamWriter(path, append: false))
            {
                file.Write(contents);
            }
        }

        public static CharacterManager ReadCharacters()
        {
            try
            {
                return ReadCharactersImpl();
            }
            catch (Exception exc)
            {
                log.WriteLogMsg("ReadCharacterImpl Exception: " + exc.ToString());
                return null;
            }
        }

        private static CharacterManager ReadCharactersImpl()
        {
            string path = FileLocations.GetCharacterFilePath();

            if (!File.Exists(path)) { return new CharacterManager(); }
            using (var file = new StreamReader(path))
            {
                string contents = file.ReadToEnd();
                var data = JsonConvert.DeserializeObject<Dictionary<string, ServerCharacterListByAccount>>(contents);
                CharacterManager charMgr = new CharacterManager(data);
                return charMgr;
            }
        }
    }
}
