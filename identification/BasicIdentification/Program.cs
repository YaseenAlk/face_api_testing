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

        const string personGroupId = "sample_group_k";
        const string personGroupName = "Person Group using the Sample Data";

        const bool make_new_grp = true;

        static ArrayList validPersonIds = new ArrayList();
        
        /* TODO: implement await for the async API calls, 
            so that transition between API calls is automatic.

        functions: CreatePersonGroup(), DefinePersonsInPersonGroup(), DefineFacesForPersons(), TrainPersonGroup(), CheckTraining(), DetectFaces(), IdentifyFaces(), OutputMatchResults()

        Flow:
        -- Start of Training methods (only needs to be done once for a unique dataset) -- 
            CreatePersonGroup() --> single API Call --> wait for response --> if finished and no error, proceed to DefinePersonsInGroup()
            DefinePersonsInPersonGroup() --> multiple PARALLEL API calls (constant) --> wait for responses --> if all finished and no error, proceed to DefineFacesForPersons()
            DefineFacesForPersons() --> multiple PARALLEL API calls (constant) --> wait for responses --> if all finished and no error, proceed to TrainPersonGroup()
            TrainPersonGroup() --> single API call --> wait for response --> if finished (API call sent, not necessarily done training) and no error, proceed to CheckTraining()
            CheckTraining() --> variable SEQUENTIAL API calls --> foreach call, send to API and wait for response --> if still training and no error, send call again; elseif done training and no error, proceed to DetectFaces()
        -- End of Training methods --

        -- Start of Identification Methods --
            DetectFaces() --> single API call --> wait for response --> store response --> proceed to IdentifyFaces()
            IdentifyFaces() --> variable PARALLEL API calls (depends on DetectFaces()) --> wait for responses --> foreach response, store identification result --> if all responses finished and no error, proceed to to OutputMatchResults()
            OutputMatchResults() --> output the result of each face --> end program
        -- End of Identification Methods --     */

        static async Task Main()
        {
            if (make_new_grp) Console.WriteLine("First, I need to create the PersonGroup. (only needs to be done the first time)");
            bool created_grp = await CreatePersonGroupAsync(make_new_grp);
            Console.WriteLine(">    created_grp: " + created_grp);
            if (created_grp) Console.WriteLine("Then, I need to define the Persons in the PersonGroup. (only needs to be done the first time)");
            bool defined_ppl = await DefinePersonsInPersonGroupAsync(created_grp);
            Console.WriteLine(">    defined_ppl: " + defined_ppl);
            if (defined_ppl) Console.WriteLine("Next, I need to detect + add faces to each Person in the PersonGroup. (only needs to be done the first time)");
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

            /* Console.WriteLine("The PersonGroup is now ready to start identifying!");
            Console.Write(
                "Enter the path to an image with faces that you wish to analyze: ");
            string imageFilePath = Console.ReadLine();

            if (File.Exists(imageFilePath))
            {
                // Execute the REST API call.
                try
                {
                    AnalyzeImage(imageFilePath);
                    Console.WriteLine("\nWait a moment for the results to appear.\n");
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n" + e.Message + "\nPress Enter to exit...\n");
                }
            }
            else
            {
                Console.WriteLine("\nInvalid file path.\nPress Enter to exit...\n");
            }
            Console.ReadLine();*/
        }

        /// <summary>
        /// 
        /// Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}
        /// CreatePersonGroup() --> single API Call --> wait for response --> compare with ideal response --> return proceed bool
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (true if the API response is expected)</returns>
        static async Task<bool> CreatePersonGroupAsync(bool proceed)
        {
            if (proceed)
            {
                string URI = uriBase + "persongroups/" + personGroupId;
                string reqBodyJSON = "{'name': '" + personGroupName +  "'}";
                byte[] reqBody = Encoding.UTF8.GetBytes(reqBodyJSON);
                bool goodResponse;

                string response = await MakeRequestAsync("Creating PersonGroup", URI, reqBody, "application/json", "PUT");
                if (response == "")
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
        /// 
        /// Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons
        /// DefinePersonsInPersonGroup() --> multiple PARALLEL API calls (constant) --> wait for responses --> compare with ideal responses --> return proceed bool
        /// </summary>
        /// <param name="proceed">Pre-determined condition to decide if the Task should run</param>
        /// <returns>The proceed condition for the next Task (true if the API response is expected)</returns>
        static async Task<bool> DefinePersonsInPersonGroupAsync(bool proceed)
        {
            if (proceed)
            {
                Dictionary<string, string> idAndResponse = await AddPersonsAsync();

                int valid = 0;  //number of valid Persons

                foreach(KeyValuePair<string, string> entry in idAndResponse)
                {
                    string rsp = entry.Value;
                    JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should just be {"personId": "..."}
                    string id = data["personId"].Value<string>();
                    if (id != "")
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

        static async Task<Dictionary<string, string>> AddPersonsAsync()
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/persons/";
            // Persons to add: Family1-Dad, Family1-Daughter, Family1-Mom, Family1-Son, Family2-Lady, Family2-Man, Family3-Lady, Family3-Man

            Dictionary<string, string> idAndResponse = new Dictionary<string, string>();

            byte[] f1Dad = Encoding.UTF8.GetBytes("{'name': 'Family1-Dad'}");
            string f1DadRsp = await MakeRequestAsync("Adding Family1-Dad to PersonGroup", URI, f1Dad, "application/json", "POST");
            idAndResponse.Add("Family1-Dad", f1DadRsp);
                
            byte[] f1Daughter = Encoding.UTF8.GetBytes("{'name': 'Family1-Daughter'}");
            string f1DaughterRsp = await MakeRequestAsync("Adding Family1-Daughter to PersonGroup", URI, f1Daughter, "application/json", "POST");
            idAndResponse.Add("Family1-Daughter", f1DaughterRsp);
                                
            byte[] f1Mom = Encoding.UTF8.GetBytes("{'name': 'Family1-Mom'}");
            string f1MomRsp = await MakeRequestAsync("Adding Family1-Mom to PersonGroup", URI, f1Mom, "application/json", "POST");
            idAndResponse.Add("Family1-Mom", f1MomRsp);
                
            byte[] f1Son = Encoding.UTF8.GetBytes("{'name': 'Family1-Son'}");
            string f1SonRsp = await MakeRequestAsync("Adding Family1-Son to PersonGroup", URI, f1Son, "application/json", "POST");
            idAndResponse.Add("Family1-Son", f1SonRsp);

            return idAndResponse;
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces[?userData][&targetFace]
        static async Task<bool> DefineFacesForPersonsAsync(bool proceed)
        {
            if (proceed)
            {
                // each ID corresponds to a Person within the sample_group PersonGroup
                // these are generated by the server every time a new Person is successfully added
            
                string generalURI = uriBase + "persongroups/" + personGroupId + "/persons/";

                string[] hardcoded_img_paths = {"../../res/SampleData/PersonGroup/Family1-Dad/Family1-Dad",
                                            "../../res/SampleData/PersonGroup/Family1-Daughter/Family1-Daughter",
                                            "../../res/SampleData/PersonGroup/Family1-Mom/Family1-Mom",
                                            "../../res/SampleData/PersonGroup/Family1-Son/Family1-Son"};
                int traverse = 0;
                int valid = 0;      //Number of valid Faces

                foreach (string id in validPersonIds)
                {
                    string URI = generalURI + id + "/persistedFaces?";
                    string current_family_member = hardcoded_img_paths[traverse];
                    for (int i = 1; i < 4; i++)
                    {
                        string imgPath = current_family_member + i + ".jpg";
                        string[] splitPath = imgPath.Split("/");
                        string imgName = splitPath[splitPath.Length - 1];

                        byte[] img = GetImageAsByteArray(imgPath);
                        string rsp = await MakeRequestAsync("Adding " + imgName + " to " + id, URI, img, "application/octet-stream", "POST");
                        
                        Console.WriteLine(">        Image path: " + imgPath);

                        JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should just be {"persistedFaceId": "..."}
                        string faceId = data["persistedFaceId"].Value<string>();
                        if (faceId != "")
                        {
                            valid++;
                            Console.WriteLine(">        persistedFaceId: " + faceId);
                        }
                        else
                        {
                            Console.WriteLine(">    There seems to be a problem with adding the img '" + imgName + "' to personId " + id);
                            Console.WriteLine(">    Response: " + rsp);
                        }
                    }
                    traverse++;
                }

                return valid > 0;
            }
            else
            {
                Console.WriteLine("Something went wrong, so DefineFacesForPersonsAsync() will not proceed.");
                return false;
            }
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/train
        static async Task<bool> TrainPersonGroupAsync(bool proceed)
        {
            if (proceed)
            {
                string URI = uriBase + "persongroups/" + personGroupId + "/train";

                byte[] empty = Encoding.UTF8.GetBytes("{}");

                string trainRsp = await MakeRequestAsync("Training the " + personGroupId + " PersonGroup using the added images", URI, empty, "application/json", "POST");
                
                if (trainRsp == "")
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

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/training
        static async Task<bool> CheckTrainingAsync()
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/training";
            
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string trainRsp = await MakeRequestAsync("Checking the status of the training", URI, empty, "application/json", "GET");
                
            JObject data = (JObject) JsonConvert.DeserializeObject(trainRsp);   //data should just be {"persistedFaceId": "..."}
            string status = data["status"].Value<string>();
            if (status != "")
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

        static async void DetectFaces()
        {}

        static async void IdentifyFaces()
        {}

        static async void OutputMatchResults()
        {}

        static string lastResponse = "";

        static async void AnalyzeImage(string path)
        {
            string URI = uriBase + "detect?";
            Dictionary<string, string> attributes = new Dictionary<string, string>();
            attributes.Add("returnFaceId", "true");

            byte[] imgData = GetImageAsByteArray(path);
            //MakeRequest("Detecting Faces in Image", URI, imgData, "application/octet-stream", "POST");
            Console.WriteLine();

            // parse imageIDs from the image we just detected
            string json = lastResponse;
            JObject[] data = (JObject[]) JsonConvert.DeserializeObject(json);    //data will be a list of Faces
            foreach (JObject face in data)
            {
                Console.WriteLine("Here's a faceId: " + face["faceId"].Value<string>());
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