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
            //string resourceName = "ApothedocImportLib.Conf.config.json";

            //string json = null;
            //Assembly assembly = Assembly.GetExecutingAssembly();
            //using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            //{
            //    using (StreamReader reader = new StreamReader(stream))
            //    {
            //        json = reader.ReadToEnd();
            //    }
            //}

            //JsonSerializerOptions options = new()
            //{
            //    PropertyNameCaseInsensitive = true
            //};

            //Config config = System.Text.Json.JsonSerializer.Deserialize<Config>(json, options);

            //return config;

            var dir = Directory.GetCurrentDirectory();
            var path = Path.Combine(dir, "config.json");

            var json  = File.ReadAllText(path);

            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            Config config= JsonConvert.DeserializeObject<Config>(json);

            return config;
        }
    }
}
