using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSHttpClientSample
{
    static class Program
    {
        const string subscriptionKey = "abe02e5cbec341c195ce55750e8b0765";

        const string uriBase =
            "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/";

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

        static void Main()
        {

            //Console.WriteLine("First, I need to create the PersonGroup. (only needs to be done the first time)");
            //CreatePersonGroup();
            //Console.ReadLine();
            //Console.WriteLine("Then, I need to define the Persons in the PersonGroup. (only needs to be done the first time)");
            //DefinePersonsInPersonGroup();
            //Console.ReadLine();
            //Console.WriteLine("Next, I need to detect + add faces to each Person in the PersonGroup. (only needs to be done the first time)");
            //DefineFacesForPersons();
            //Console.ReadLine();
            //Console.WriteLine("Finally, I need to train the PersonGroup.");
            //TrainPersonGroup();
            //Console.ReadLine();

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

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}
        static async void CreatePersonGroup()
        {
            string personGroupId = "sample_group";
            string personGroupName = "Person Group using the Sample Data";
            string URI = uriBase + "persongroups/" + personGroupId;
            string reqBodyJSON = "{'name': '" + personGroupName +  "'}";
            byte[] reqBody = Encoding.UTF8.GetBytes(reqBodyJSON);

            MakeRequest("Creating PersonGroup", URI, reqBody, "application/json", "PUT");
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons
        static async void DefinePersonsInPersonGroup()
        {
            string personGroupId = "sample_group";
            string URI = uriBase + "persongroups/" + personGroupId + "/persons/";
            // Persons to add: Family1-Dad, Family1-Daughter, Family1-Mom, Family1-Son, Family2-Lady, Family2-Man, Family3-Lady, Family3-Man

            byte[] f1Dad = Encoding.UTF8.GetBytes("{'name': 'Family1-Dad'}");
            MakeRequest("Adding Family1-Dad to PersonGroup", URI, f1Dad, "application/json", "POST");
            Console.ReadLine();

            byte[] f1Daughter = Encoding.UTF8.GetBytes("{'name': 'Family1-Daughter'}");
            MakeRequest("Adding Family1-Daughter to PersonGroup", URI, f1Daughter, "application/json", "POST");
            Console.ReadLine();

            byte[] f1Mom = Encoding.UTF8.GetBytes("{'name': 'Family1-Mom'}");
            MakeRequest("Adding Family1-Mom to PersonGroup", URI, f1Mom, "application/json", "POST");
            Console.ReadLine();

            byte[] f1Son = Encoding.UTF8.GetBytes("{'name': 'Family1-Son'}");
            MakeRequest("Adding Family1-Son to PersonGroup", URI, f1Son, "application/json", "POST");
            
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces[?userData][&targetFace]
        static async void DefineFacesForPersons()
        {
            // each ID corresponds to a Person within the sample_group PersonGroup
            // these are generated by the server every time a new Person is successfully added
            string dadID = "9fccaee3-a01b-4791-a8de-52d803ce9f13";
            string sisID = "cdfa62bc-7d45-4b31-a39c-589c38f95c16";
            string momID = "43b2a4ba-a044-4617-8c37-a022a4e6e26e";
            string sonID = "6528b9a0-54e3-4156-90c4-e316cc6e4ecc";
            
            string personGroupId = "sample_group";
            string generalURI = uriBase + "persongroups/" + personGroupId + "/persons/";

            string dadURI = generalURI + dadID + "/persistedFaces?";
            string sisURI = generalURI + sisID + "/persistedFaces?";
            string momURI = generalURI + momID + "/persistedFaces?";
            string sonURI = generalURI + sonID + "/persistedFaces?";

            byte[] dad1 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Dad/Family1-Dad1.jpg");
            MakeRequest("Adding Dad1 pic to Family1-Dad", dadURI, dad1, "application/octet-stream", "POST");
            Console.ReadLine();

            byte[] dad2 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Dad/Family1-Dad2.jpg");
            MakeRequest("Adding Dad2 pic to Family1-Dad", dadURI, dad2, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] dad3 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Dad/Family1-Dad3.jpg");
            MakeRequest("Adding Dad3 pic to Family1-Dad", dadURI, dad3, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] sis1 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Daughter/Family1-Daughter1.jpg");
            MakeRequest("Adding Daughter1 pic to Family1-Daughter", sisURI, sis1, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] sis2 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Daughter/Family1-Daughter2.jpg");
            MakeRequest("Adding Daughter2 pic to Family1-Daughter", sisURI, sis2, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] sis3 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Daughter/Family1-Daughter3.jpg");
            MakeRequest("Adding Daughter3 pic to Family1-Daughter", sisURI, sis3, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] mom1 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Mom/Family1-Mom1.jpg");
            MakeRequest("Adding Mom1 pic to Family1-Mom", momURI, mom1, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] mom2 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Mom/Family1-Mom2.jpg");
            MakeRequest("Adding Mom2 pic to Family1-Mom", momURI, mom2, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] mom3 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Mom/Family1-Mom3.jpg");
            MakeRequest("Adding Mom3 pic to Family1-Mom", momURI, dad3, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] son1 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Son/Family1-Son1.jpg");
            MakeRequest("Adding Son1 pic to Family1-Son", sonURI, son1, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] son2 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Son/Family1-Son2.jpg");
            MakeRequest("Adding Son2 pic to Family1-Son", sonURI, son2, "application/octet-stream", "POST");
            Console.ReadLine();
            
            byte[] son3 = GetImageAsByteArray("../../res/SampleData/PersonGroup/Family1-Son/Family1-Son3.jpg");
            MakeRequest("Adding Son3 pic to Family1-Son", sonURI, son3, "application/octet-stream", "POST");
        }

        //Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/train
        static async void TrainPersonGroup()
        {
            string personGroupId = "sample_group";
            string URI = uriBase + "persongroups/" + personGroupId + "/train";

            byte[] empty = Encoding.UTF8.GetBytes("{}");

            MakeRequest("Training the sample_group PersonGroup using the added images", URI, empty, "application/json", "POST");
        }

        static async void CheckTraining()
        {}

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
            MakeRequest("Detecting Faces in Image", URI, imgData, "application/octet-stream", "POST");
            Console.WriteLine();

            Console.WriteLine("Here's what lastResponse contains:");
            Console.WriteLine(lastResponse);

            // parse imageIDs from the image we just detected
            string json = lastResponse;
            JObject[] data = (JObject[])JsonConvert.DeserializeObject(json);    //data will be a list of Faces
            foreach (JObject face in data)
            {
                Console.WriteLine("Here's a faceId: " + face["faceId"].Value<string>());
            }
        }

        static async void MakeRequest(string purpose, string uri, byte[] reqBodyData, string bodyContentType, string method, Dictionary<string, string> requestParameters = null)
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

            Console.WriteLine("Full URI: " + fullUri);  // debug line

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
                else
                {
                    response = await client.PutAsync(fullUri, content);
                }
                

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();

                // Display the JSON response.
                Console.WriteLine("\nResponse for " + purpose + ":\n");
                Console.WriteLine(JsonPrettyPrint(contentString));
                Console.WriteLine("\nPress any key to continue...");  //debug line

                lastResponse = await response.Content.ReadAsStringAsync();
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