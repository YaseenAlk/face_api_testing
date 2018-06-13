using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetFirstPersonIdFromName
{
    class Program
    {
        static readonly string subscriptionKey = ReadJsonStrFromFile("api_access_key.txt", "subscriptionKey");
        static readonly string uriBase = ReadJsonStrFromFile("api_access_key.txt", "uriBase");

        static readonly string[] SUPPORTED_ACTIONS = {"-getId", "-createPerson"};

        // Usage: dotnet TerminalUtils.dll -getId <personGroupId> <name>
        // Usage: dotnet TerminalUtils.dll -createPerson <personGroupId> <name> <user data (optional)>
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: dotnet TerminalUtils.dll [action] [supporting args]");
                return;
            }

            string action = args[0].ToLower();

            if (action == "-getid")
            {
                if (args.Length != 3)
                {
                    Console.WriteLine("Usage: dotnet GetFirstPersonIdFromName.dll <personGroupId> <name>");
                    return;
                }
                string personGroupId = args[1];
                string name = args[2];
                string firstId = await GetFirstIdFromNameAsync(personGroupId, name);
                Console.WriteLine(firstId);
                return;
            }
            else if (action == "-createperson")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: dotnet TerminalUtils.dll -createPerson <personGroupId> <name> <user data (optional)>");
                    return;
                }
                string personGroupId = args[1];
                string name = args[2];
                string data = (args.Length > 3) ? args[3] : "";
                string returnedId = await CreatePersonAsync(personGroupId, name, data);
                Console.WriteLine(returnedId);
                return;
            }
            else
            {
                Console.WriteLine("That action is not currently supported.");
                Console.WriteLine("Supported actions: " + String.Join(", ", SUPPORTED_ACTIONS));
                return;
            }
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons
        static async Task<string> CreatePersonAsync(string grp, string name, string data)
        {
            string URI = uriBase + "persongroups/" + grp + "/persons";
            byte[] encoded = Encoding.UTF8.GetBytes("{'name': '" + name + "', 'userData': '" + data + "'}");
            string rsp = await MakeRequestAsync("Adding Person to Person Group", URI, encoded, "application/json", "POST");
            JObject returnedData = (JObject) JsonConvert.DeserializeObject(rsp);
            string id = returnedData["personId"].Value<string>();
            return (id != null) ? id : "null";
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons
        static async Task<string> GetFirstIdFromNameAsync(string grp, string name)
        {
            string URI = uriBase + "persongroups/" + grp + "/persons";
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string rsp = await MakeRequestAsync("List all Persons in PersonGroup", URI, empty, "application/json", "GET");
            JArray ppl = (JArray) JsonConvert.DeserializeObject(rsp);
            
            foreach (JObject person in ppl)
            {
                string personName = person["name"].Value<string>();
                string id = person["personId"].Value<string>();
                if (name == personName)
                {
                    return id;
                }
            }
            return "null";
        }

        static async Task<string> MakeRequestAsync(string purpose, string uri, byte[] reqBodyData, string bodyContentType, string method, Dictionary<string, string> requestParameters = null)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            if (requestParameters != null)
            {
                foreach (string key in requestParameters.Keys)
                {
                    queryString[key] = requestParameters[key];
                }
            }

            var fullUri = uri + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = reqBodyData;

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(bodyContentType);
                if (method.ToLower() == "post")
                {
                    response = await client.PostAsync(fullUri, content);
                }
                else if (method.ToLower() == "get")
                {
                    response = await client.GetAsync(fullUri);
                }
                else
                {
                    response = await client.PutAsync(fullUri, content);
                }
                

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                return contentString;
            }
        }

        static string ReadJsonStrFromFile(string path, string param)
        {
            string json = System.IO.File.ReadAllText(path);
            JObject data = (JObject) JsonConvert.DeserializeObject(json);
            return data[param].Value<string>();
        }
    }
}
