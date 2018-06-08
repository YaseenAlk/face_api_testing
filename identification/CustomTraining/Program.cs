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

namespace CSHttpClientSample
{
    static class Program
    {
        const string subscriptionKey = "a43530f777ee45599a06535c39b2fe4f";

        const string uriBase =
            "https://eastus.api.cognitive.microsoft.com/face/v1.0/";

        static string personGroupId = "sample_group_k";
        static string personGroupName = "Person Group using the Sample Data";

        const bool make_new_grp = false;

        static ArrayList validPersonIds = new ArrayList();
        static bool pre_existing_grp = false;
        
        /* 
        functions: CreatePersonGroup(), DefinePersonsInPersonGroup(), DefineFacesForPersons(), TrainPersonGroup(), CheckTraining(), DetectFaces(), IdentifyFaces(), OutputMatchResults()

        Flow:
        -- Start of Training methods (only needs to be done once for a unique dataset) -- 
            CreatePersonGroup() if make_new_grp is true (single request) 
            --> DefinePersonsInPersonGroup() if CreatePersonGroup() is successful (multiple requests in parallel) 
            --> DefineFacesForPersons() if DefinePersonsInPersonGroup() is successful (multiple requests in sequence)
            --> TrainPersonGroup() if DefineFacesForPersons() is successful (single request)
            --> CheckTraining() if TrainPersonGroup is successful (single request) (in practice, used continuously in a loop until desired response is received)
        -- End of Training methods -- */

        static async Task Main()
        {
            Console.Write("Enter the name of the PersonGroup you would like to train: ");
            personGroupId = Console.ReadLine();
            Console.WriteLine(personGroupId);
            pre_existing_grp = await CheckIfGrpExistsAsync(personGroupId);
            bool created_grp;
            if (!pre_existing_grp) {
                Console.Write("There is no PersonGroup with the id \"" + personGroupId + "\". Would you like to create it? (y/n): ");
                string create = Console.ReadLine();
                if (!create.StartsWith("y"))
                {
                    created_grp = false;
                }
                else
                {
                    Console.WriteLine("Enter a display name for the \"" + personGroupId + "\" PersonGroup (max. length is 128 characters): ");
                    personGroupName = Console.ReadLine();
                    Console.WriteLine("Creating the PersonGroup... ");
                    created_grp = await CreatePersonGroupAsync(true);
                    if (created_grp) Console.WriteLine("Done creating the PersonGroup.");
                    else Console.WriteLine("Something went wrong trying to create the PersonGroup.");
                }
            }
            else
            {
                created_grp = true;
            }

            bool defined_ppl = await DefinePersonsInPersonGroupAsync(created_grp);
            bool defined_faces = await DefineFacesForPersonsAsync(defined_ppl);

            if (defined_faces) Console.WriteLine("Finally, I need to train the PersonGroup.");
            bool startedTraining = await TrainPersonGroupAsync(defined_faces);
            if (startedTraining)
            {
                Console.WriteLine("Checking if training is completed... ");
                bool finishedTraining = await CheckTrainingAsync();
                while (!finishedTraining)
                {
                    finishedTraining = await CheckTrainingAsync();
                }
                Console.WriteLine("Training complete!");
            }
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}
        static async Task<bool> CheckIfGrpExistsAsync(string grp)
        {
            string URI = uriBase + "persongroups/" + grp;
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string rsp = await MakeRequestAsync("Checking if PersonGroup '" + grp + "' exists", URI, empty, "application/json", "GET");
            JObject data = (JObject) JsonConvert.DeserializeObject(rsp);
            var result = data["personGroupId"];

            return result != null && result.Value<string>() == grp;
        }

        /// <summary>
        /// Sends a single HTTP PUT request to create a new PersonGroup.
        ///
        /// Flow:
        /// CreatePersonGroupAsync() --> single API Call --> wait for response --> compare with ideal response --> return proceed bool
        ///
        /// Request URL: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}
        ///
        /// Refer to Microsoft's documentation for specific error responses:
        /// https://eastus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f30395244
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (True if the API response is expected)</returns>
        static async Task<bool> CreatePersonGroupAsync(bool proceed)
        {
            if (proceed)
            {
                string URI = uriBase + "persongroups/" + personGroupId;
                string reqBodyJSON = "{'name': '" + personGroupName +  "'}";
                byte[] reqBody = Encoding.UTF8.GetBytes(reqBodyJSON);

                string response = await MakeRequestAsync("Creating PersonGroup", URI, reqBody, "application/json", "PUT");
                if (response == "")     //ideal response: empty string
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Something went wrong, so CreatePersonGroupAsync() will not proceed.");
                return false;
            }
            
        }

        /// <summary>
        /// Sends multiple HTTP POST requests to add Persons to the PersonGroup.
        ///
        /// Flow:
        /// DefinePersonsInPersonGroupAsync() --> use AddPersonsAsync() to add Persons and receive responses --> compare with ideal responses --> return proceed bool
        ///
        /// Request URL: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons
        /// 
        /// Refer to Microsoft's documentation for specific error responses:
        /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f3039523c
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (True if at least one API response is expected)</returns>
        static async Task<bool> DefinePersonsInPersonGroupAsync(bool proceed)
        {
            if (proceed)
            {
                if (pre_existing_grp)
                {
                    await RetrieveExistingPersonsAsync();
                }

                Dictionary<string, string> idAndResponse = await AddPersonsAsync();

                int valid = 0;  //number of valid Persons

                foreach(KeyValuePair<string, string> entry in idAndResponse)
                {
                    string rsp = entry.Value;
                    JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should just be {"personId": "..."}
                    string id = data["personId"].Value<string>();
                    if (id != "")   //ideal response: a JSON string with a "personId" field
                    {
                        valid++;
                        Console.WriteLine(">    personId: " + id);
                        validPersonIds.Add(id);
                    }
                    else
                    {
                        Console.WriteLine(">    There seems to be a problem with Defining '" + entry.Key + "'");
                        Console.WriteLine(">    Response: " + rsp);
                    }
                }

                return valid > 0;

            }
            else
            {
                Console.WriteLine("Something went wrong, so DefinePersonsInPersonGroupAsync() will not proceed.");
                return false;
            }
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons[?start][&top]
        static async Task RetrieveExistingPersonsAsync()
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/persons";
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string listRsp = await MakeRequestAsync("Retrieving existing Persons", URI, empty, "application/json", "GET");
            JArray people = (JArray) JsonConvert.DeserializeObject(listRsp);   //data should be [{"personId": "...", ...},{"personId":"...", ...}] 
            foreach (JObject person in people)
            {
                validPersonIds.Add(people["personId"].Value<string>());
            }
        }

        /// <summary>
        /// Helper method for DefinePersonsInPersonGroupAsync()
        ///
        /// Sends multiple HTTP POST requests to add the pre-defined Persons to the PersonGroup. All requests are sent in parallel.
        ///
        /// Microsoft documentation:
        /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f3039523c 
        /// </summary>
        /// <returns>A string/string Dictionary with attempted Persons. The Person name (NOT personId) is the Key and the HTTP response is the Value.</returns>
        static async Task<Dictionary<string, string>> AddPersonsAsync()
        {
            Console.Write("How many Persons would you like to add to the PersonGroup?: ");
            int num;
            bool success = Int32.TryParse(Console.ReadLine(), out num);
            
            if (success)
            {
                Dictionary<string, string> newPpl = new Dictionary<string, string>();   //dictionary to store name and custom data (before the request)
            
                for (int i = 0; i < num; i++)
                {
                    Console.Write("Enter a name for Person #" + i + " (case-sensitive; does not need to be unique; limit 128 chars): ");
                    string name = Console.ReadLine();
                    Console.Write("Enter any user-data/custom description for " + name + " (optional; size limit is 16KB): ");
                    string data = Console.ReadLine();

                    if (name != "")
                        newPpl.Add(name, data);
                    else
                    {
                        Console.WriteLine("The name needs to be at least one character long.");
                        i--;
                    }
                }

                //currently, the HTTP requests are all sent after *all* new Persons have been defined
                //alternatively, it's possible to have each request sent after each Person is defined

                string URI = uriBase + "persongroups/" + personGroupId + "/persons/";
            
                Dictionary<string, string> idAndResponse = new Dictionary<string, string>();    //dictionary to store personId and HTTP response (after the request)

                foreach (KeyValuePair<string, string> newPerson in newPpl)
                {
                    byte[] encoded = Encoding.UTF8.GetBytes("{'name': '" + newPerson.Key + "', 'userData': '" + newPerson.Value + "'}");
                    string rsp = await MakeRequestAsync("Adding Person '" + newPerson.Key + "' to PersonGroup", URI, encoded, "application/json", "POST");
                    idAndResponse.Add(newPerson.Key, rsp);
                }
                return idAndResponse;
            }
            else
            {
                return await AddPersonsAsync();
            }
        }

        /// <summary>
        /// Sends multiple HTTP POST requests to add Faces (upload images) for each Person in the PersonGroup
        ///
        /// Flow:
        /// DefineFacesForPersons() --> multiple sequential API calls --> wait for responses --> if all finished and no error, proceed to TrainPersonGroup()
        ///
        /// Request URL: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces[?userData][&targetFace]
        ///
        /// Refer to Microsoft's documentation for specific error responses:
        /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f3039523b
        ///
        /// Note: There is the potential for this method to be rewritten so that the requests are sent in parallel.
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (True if at least one API response is expected)</returns>
        static async Task<bool> DefineFacesForPersonsAsync(bool proceed)
        {
            if (proceed)
            {
                foreach (string id in validPersonIds)
                {
                    string name = await IdToNameAsync(id);
                    if (name != "")
                    {
                        int faceNum = HowManyFaces(id, name);
                        for (int i = 0; i < faceNum; i++)
                        {
                            Console.Write("Enter the image path for " + name + "'s Face #" + i + ": ");
                            string path = Console.ReadLine();
                            bool works = await UploadImage(id, path);
                            
                            if (works)
                            {
                                Console.WriteLine("Image #" + i + " for " + name + " successfully uploaded!");
                            }
                            else
                            {
                                Console.WriteLine("Something went wrong uploading Image #" + i + " for " + name);
                                i--;
                            }
                        }
                    }
                    else    //no name associated with the id? API error?
                    {
                        //todo: handle this case
                    }
                }
                return true;
            }
            else
            {
                Console.WriteLine("Something went wrong, so DefineFacesForPersonsAsync() will not proceed.");
                return false;
            }
        }

        static async Task<bool> UploadImage(string id, string path)
        {
            if (File.Exists(path))
            {
                string URI = uriBase + "persongroups/" + personGroupId + "/persons/" + id + "/persistedFaces?";

                string[] splitPath = path.Split("/");
                string imgName = splitPath[splitPath.Length - 1];

                byte[] img = GetImageAsByteArray(path);
                string rsp = await MakeRequestAsync("Adding " + imgName + " to " + id, URI, img, "application/octet-stream", "POST");

                JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should just be {"persistedFaceId": "..."}
                string faceId = data["persistedFaceId"].Value<string>();
                if (faceId != "")   //ideal response: a JSON string with a "persistedFaceId" field
                {
                    Console.WriteLine(">        persistedFaceId: " + faceId);
                }
                else
                {
                    Console.WriteLine(">    There seems to be a problem with adding the img '" + imgName + "' to personId " + id);
                    Console.WriteLine(">    Response: " + rsp);
                }

                return faceId != "";
            }
            else
            {
                Console.WriteLine("Invalid file path.");
                return false;
            }
        }

        static int HowManyFaces(string id, string name)
        {
            Console.Write("Enter the number of Faces (images) would you like to upload for " + name + " (personId: " + id + "): ");
            int num;
            bool success = Int32.TryParse(Console.ReadLine(), out num);
            if (success)
            {
                return num;
            }
            else
            {
                Console.WriteLine("Please enter a valid number of Faces.");
                return HowManyFaces(id, name);
            }
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}
        static async Task<string> IdToNameAsync(string id)
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/persons/" + id;
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string rsp = await MakeRequestAsync("Retrieve Person associated with ID", URI, empty, "application/json", "GET");
            JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should be {"personId": "...", ...}
            return data["name"].Value<string>();    //todo: make this error-proof
        }

        /// <summary>
        /// Sends a single HTTP POST request to *start* training the API with the PersonGroup. Note that the response may be received before training is complete.
        ///
        /// Flow:
        /// TrainPersonGroupAsync() --> single API Call --> wait for response --> compare with ideal response --> return proceed bool
        ///
        /// Request URL: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/train
        ///
        /// Refer to Microsoft's documentation for specific error responses:
        /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f30395249
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (True if the API response is expected)</returns>
        static async Task<bool> TrainPersonGroupAsync(bool proceed)
        {
            if (proceed)
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
            else 
            {
                Console.WriteLine("Something went wrong, so TrainPersonGroupAsync() will not proceed.");
                return false;
            }
        }

        /// <summary>
        /// Sends a single HTTP GET request to receive the current training status.
        /// Possible statuses: "notstarted", "running", "succeeded", or "failed"
        /// 
        /// Flow:
        /// CheckTrainingAsync() --> single API Call --> wait for response --> compare with ideal response --> return bool
        ///
        /// Request URL: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/training
        ///
        /// Refer to Microsoft's documentation for more information:
        /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f30395247
        /// 
        /// Note that this method is only invoked if TrainPersonGroupAsync has been invoked 
        /// and if the Training request's asynchronous Task has been completed.
        /// Therefore, there is no need for a "proceed" boolean argument.
        /// </summary>
        /// <returns>True if the request is successfully sent and the received status is "succeeded"; False for all other cases</returns>
        static async Task<bool> CheckTrainingAsync()
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/training";
            
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string trainRsp = await MakeRequestAsync("Checking the status of the training", URI, empty, "application/json", "GET");
                
            JObject data = (JObject) JsonConvert.DeserializeObject(trainRsp);   //data should just be {"persistedFaceId": "..."}
            string status = data["status"].Value<string>();
            if (status != "")       //ideal response: a JSON string with a "status" field
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

            Console.WriteLine(">    Full URI: " + fullUri);  // debug line

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

                // Display the JSON response.
                //Console.WriteLine("\nResponse for " + purpose + ":\n");
                //Console.WriteLine(JsonPrettyPrint(contentString));
                //Console.WriteLine("\nPress any key to continue...");  //debug line

                return contentString;
            }
        }


        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }


        /// <summary>
        /// Formats the given JSON string by adding line breaks and indents.
        /// </summary>
        /// <param name="json">The raw JSON string to format.</param>
        /// <returns>The formatted JSON string.</returns>
        static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            json = json.Replace(Environment.NewLine, "").Replace("\t", "");

            StringBuilder sb = new StringBuilder();
            bool quote = false;
            bool ignore = false;
            int offset = 0;
            int indentLength = 3;

            foreach (char ch in json)
            {
                switch (ch)
                {
                    case '"':
                        if (!ignore) quote = !quote;
                        break;
                    case '\'':
                        if (quote) ignore = !ignore;
                        break;
                }

                if (quote)
                    sb.Append(ch);
                else
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', ++offset * indentLength));
                            break;
                        case '}':
                        case ']':
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', --offset * indentLength));
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.Append(Environment.NewLine);
                            sb.Append(new string(' ', offset * indentLength));
                            break;
                        case ':':
                            sb.Append(ch);
                            sb.Append(' ');
                            break;
                        default:
                            if (ch != ' ') sb.Append(ch);
                            break;
                    }
                }
            }

            return sb.ToString().Trim();
        }
    }
}