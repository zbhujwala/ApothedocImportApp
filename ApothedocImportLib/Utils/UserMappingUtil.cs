using ApothedocImportLib.DataItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class UserMappingUtil
    {
        public List<UserIdMapping> LoadJsonFile()
        {
            string resourceName = "ApothedocImportLib.conf.user-mapping.json";

            string json = null;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }
            }

            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };

            UserIdMappingWrapper userMappings = System.Text.Json.JsonSerializer.Deserialize<UserIdMappingWrapper>(json, options);

            return userMappings.Mappings;
        }
    }
}
