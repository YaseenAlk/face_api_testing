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

namespace TerminalUtils
{
    class Program
    {
        // TODO: make this program error-proof
        static readonly string subscriptionKey = ReadJsonStrFromFile("api_access_key.txt", "subscriptionKey");
        static readonly string uriBase = ReadJsonStrFromFile("api_access_key.txt", "uriBase");

        static readonly string[] SUPPORTED_ACTIONS = {"-getId", "-createPerson", "-uploadImages", "-train"};

        static bool no_output = false;

        // Usage: dotnet TerminalUtils.dll -getId <personGroupId> <name>
        // Usage: dotnet TerminalUtils.dll -createPerson <personGroupId> <name> <user data (optional)>
        // Usage: dotnet TerminalUtils.dll -uploadImages <personGroupId (must already exist)> <personId (must already exist)> <image path dir> [-no_output (optional)]
        // Usage: dotnet TerminalUtils.dll -train <personGroupId>
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
                    Console.WriteLine("Usage: dotnet TerminalUtils.dll -getId <personGroupId> <name>");
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
            else if (action == "-uploadimages")
            {
                if (args.Length < 4)
                {
                    Console.WriteLine("Usage: dotnet TerminalUtils.dll -uploadImages <personGroupId (must already exist)> <personId (must already exist)> <image path dir> [-no_output (optional)]");
                    return;
                }
                if (Array.IndexOf(args, "-no_output") > -1)
                {
                    no_output = true;
                }

                string personGroupId = args[1];
                string personId = args[2];
                string path = args[3];
                
                if (!Directory.Exists(path))
                {
                    Console.WriteLine("Invalid image path.");
                    return;
                }

                await DefineFacesForPersonsAsync(personGroupId, personId, path);
                return;
            }
            else if (action == "-train")
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Usage: dotnet TerminalUtils.dll -train <personGroupId>");
                    return;
                }

                string personGroupId = args[1];

                bool startedTraining = await TrainPersonGroupAsync(personGroupId);
                if (startedTraining)
                {
                    Console.WriteLine("Checking if training is completed... ");
                    bool finishedTraining = await CheckTrainingAsync(personGroupId);
                    while (!finishedTraining)
                    {
                        finishedTraining = await CheckTrainingAsync(personGroupId);
                    }
                    Console.WriteLine("Training complete!");
                }
            }
            else
            {
                Console.WriteLine("That action is not currently supported.");
                Console.WriteLine("Supported actions: " + String.Join(", ", SUPPORTED_ACTIONS));
                return;
            }
        }

        static async Task<bool> TrainPersonGroupAsync(string personGroupId)
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/train";

            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string trainRsp = await MakeRequestAsync("Training the " + personGroupId + " PersonGroup using the added images", URI, empty, "application/json", "POST");
                
            if (trainRsp == "")     //ideal response: empty string
            {
                return true;
            }
            else
            {
                Console.WriteLine("Training Request failed.");
                Console.WriteLine("Response: " + trainRsp);
                return false;
            }
        }

        static async Task<bool> CheckTrainingAsync(string personGroupId)
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/training";
            
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string trainRsp = await MakeRequestAsync("Checking the status of the training", URI, empty, "application/json", "GET");
                
            JObject data = (JObject) JsonConvert.DeserializeObject(trainRsp);
            string status = data["status"].Value<string>();
            if (status != null && status != "")       //ideal response: a JSON string with a "status" field
            {
                Console.WriteLine(">        (training) status: " + status);
                
                return status == "succeeded";
            }
            else
            {
                Console.WriteLine(">    There seems to be a problem with requesting the training status of personGroupId '" + personGroupId + "'");
                Console.WriteLine(">    Response: " + trainRsp);
                return false;
            }
        }

        static async Task DefineFacesForPersonsAsync(string personGroupId, string personId, string imagePath)
        {
            string[] dirs = Directory.GetFiles(imagePath, "*.bmp");
            if (!no_output) Console.WriteLine("There are " + dirs.Length + " frames in this directory...");
            foreach (string img in dirs)
            {
                await UploadImage(personGroupId, personId, img);
            }
        }

        static async Task UploadImage(string personGroupId, string id, string path)
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/persons/" + id + "/persistedFaces?";

            string[] splitPath = path.Split("/");
            string imgName = splitPath[splitPath.Length - 1];

            byte[] img = GetImageAsByteArray(path);
            string rsp = await MakeRequestAsync("Adding " + imgName + " to " + id, URI, img, "application/octet-stream", "POST");

            JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should just be {"persistedFaceId": "..."}
            string faceId = data["persistedFaceId"].Value<string>();
            if (faceId == null || faceId == "")
            {
                if (!no_output) Console.WriteLine(">    There seems to be a problem with adding the img '" + imgName + "'");
                if (!no_output) Console.WriteLine(">    Response: " + rsp);
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

        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }
    }
}
