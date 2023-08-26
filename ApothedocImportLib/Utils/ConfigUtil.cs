using ApothedocImportLib.DataItem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class ConfigUtil
    {
        public Config LoadConfig()
        {

            var dir = Directory.GetCurrentDirectory();
            var path = Path.Combine(dir, "config.json");

            var json  = File.ReadAllText(path);

            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            Config config = JsonConvert.DeserializeObject<Config>(json);

            return config;
        }
    }
}
