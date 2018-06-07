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

        static string personGroupId;
        
        /* 
        functions: DetectFaces(), IdentifyFaces(), OutputMatchResults()

        Flow:
        -- Start of Identification Methods --
-            DetectFaces() --> single API call --> wait for response --> store response --> proceed to IdentifyFaces()
-            IdentifyFaces() --> multiple PARALLEL API calls (number depends on DetectFaces()) --> wait for responses --> foreach response, store identification result --> if all responses finished and no error, proceed to to OutputMatchResults()
-            OutputMatchResults() --> output the result of each face --> end program
-        -- End of Identification Methods -- */

        static async Task Main()
        {
            Console.WriteLine("Now that training is complete, the PersonGroup is ready to start identifying!");
            Console.Write("Enter the personGroupId that we are comparing with: ");
            personGroupId = Console.ReadLine();

            Console.Write("Enter the path to an image with faces that you wish to analyze: ");
            string imageFilePath = Console.ReadLine();

            List<string> detectedFaceIds;

            if (File.Exists(imageFilePath))
            {
                try
                {
                    detectedFaceIds = await DetectFacesAsync(imageFilePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n" + e.Message);
                    detectedFaceIds = null;
                }
            }
            else
            {
                Console.WriteLine("\nInvalid file path.");
                detectedFaceIds = null;
            }

            if (detectedFaceIds != null)
            {
                Dictionary<string, Dictionary<string, float>> results = await IdentifyAsync(detectedFaceIds);

                //OutputResults doesn't necessarily need to be async;
                //it's only async here because I do an extra step that maps the personId back to their name (after results are calculated)
                await OutputResultsAsync(results);
            }
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/detect[?returnFaceId][&returnFaceLandmarks][&returnFaceAttributes]
        static async Task<List<string>> DetectFacesAsync(string imgPath)
        {
            string URI = uriBase + "detect";
            byte[] img = GetImageAsByteArray(imgPath);
            List<string> detectedIds = new List<string>();

            string rsp = await MakeRequestAsync("Detect faces in an image", URI, img, "application/octet-stream", "POST");
            //response will be a list of faces: [{"faceId":"...", ...}, {"faceId":"...", ...}]
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);   //data should just be {"personId": "..."}
            foreach (JObject face in data)
            {
                detectedIds.Add(face["faceId"].Value<string>());
            }

            return detectedIds;
        }

        static async Task<Dictionary<string, Dictionary<string, float>>> IdentifyAsync(List<string> detectedIds)
        {
            Dictionary<string, Dictionary<string, float>> results = new Dictionary<string, Dictionary<string, float>>();
            foreach (string id in detectedIds)
            {
                string rsp = await IdentifyFaceAsync(id);
                JArray data = (JArray) JsonConvert.DeserializeObject(rsp);
                //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

                JObject faceAndCandidates = (JObject) data[0];    //single face, potentially many candidates
                Dictionary<string, float> idsAndConfidences = new Dictionary<string, float>();
                
                JArray candidates = (JArray) faceAndCandidates["candidates"];
                foreach (JObject cand in candidates)
                {
                    idsAndConfidences.Add(cand["personId"].Value<string>(), cand["confidence"].Value<float>());
                }

                results.Add(faceAndCandidates["faceId"].Value<string>(), idsAndConfidences);
            }
            return results;
        }

        static async Task OutputResultsAsync(Dictionary<string, Dictionary<string, float>> results)
        {
            //keys are detected faces; values are the list of candidates for each face
            int faceNum = 0;
            foreach(KeyValuePair<string, Dictionary<string, float>> entry in results)
            {
                Console.WriteLine("---- Detected Face #" + faceNum + " ----");
                string faceId = entry.Key;  //currently not using the faceId for anything. might be useful in the future
                Dictionary<string, float> candidates = entry.Value;
                
                foreach(KeyValuePair<string, float> cand in candidates)
                {
                    string candName = await IdToNameAsync(cand.Key);
                    float confidence = cand.Value;
                    if (candName != null)   //not sure if this is the proper way to wait for the task
                    {
                        Console.WriteLine("I am " + (confidence * 100) + "% sure that you are " + candName);
                    }
                }
                Console.WriteLine("--------------------------");
                faceNum++;
            }
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/identify
        static async Task<string> IdentifyFaceAsync(string faceId)
        {
            //TODO: the API is capable of handling 10 independent faces in one call of "Face - Identify"
            //currently, I am putting one face per call because it guarantees that every call will have <10 faces
            //it might be beneficial to rewrite this such that the number of API calls is minimized 
            //(and the number of faces put into each call is maximized)
            string URI = uriBase + "identify";
            string reqBody = "{\"personGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + faceId + "\"]}";
            byte[] req = Encoding.UTF8.GetBytes(reqBody);

            string rsp = await MakeRequestAsync("Identify person using faceId", URI, req, "application/json", "POST");
            return rsp;
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}
        static async Task<string> IdToNameAsync(string id)
        {
            string URI = uriBase + "persongroups/" + personGroupId + "/persons/" + id;
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string rsp = await MakeRequestAsync("Retrieve Person associated with ID", URI, empty, "application/json", "GET");
            JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should be {"personId": "...", ...}
            return data["name"].Value<string>();    //todo: make this error-prone
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