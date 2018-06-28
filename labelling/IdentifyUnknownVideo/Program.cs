using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdentifyUnknownVideo
{
    static class Program
    {
        static readonly string subscriptionKey = ReadJsonStrFromFile("api_access_key.txt", "subscriptionKey");
        static readonly string uriBase = ReadJsonStrFromFile("api_access_key.txt", "uriBase");

        const string FILE_EXTENSION = ".bmp";
        static string personGroupId, frameFolderPath, outputPath;

        static bool del_after_processing = false;

        // dictionary: each key is a candidate's personId and each Value is a list of confidence vals
        static Dictionary<string, List<decimal>> averagedGuesses = new Dictionary<string, List<decimal>>();

        // key is their unique microsoft-assigned personID; value is the name associated with the personID
        static Dictionary<string, string> candidateNames = new Dictionary<string, string>();

        // Functionality:
        // Given: personGroupId, frameFolderPath, outputPath (assuming that all three already exist and are pre-processed)
        // Output: For every frame in frameFolderPath, make a .txt file (same name) with guesses + confidences
        // (and delete the frame file if del_after_processing is true)
        // Also output a file called averaged.txt that has the averaged confidence levels from every frame

        // Usage: dotnet IdentifyUnknownVideo.dll <personGroupId> <frame folder path> <output path for guess folder> [-del_after_processing (optional)]
        static async Task Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: dotnet IdentifyUnknownVideo.dll <personGroupId> <frame folder path> <output path for guess folder> [-del_after_processing (optional)]");
                return;
            }

            if (subscriptionKey == "" || uriBase == "")
            {
                Console.WriteLine("Please make sure that api_access_key.txt is in the correct location.");
                return;
            }

            if (Array.IndexOf(args, "-del_after_processing") > -1)
            {
                del_after_processing = true;
            }

            // todo: sanitize these inputs later? going to assume they're all correct for now...
            personGroupId = args[0].ToLower();  //force lowercase, just in case!
            frameFolderPath = args[1];
            outputPath = args[2];
            
            if (!frameFolderPath.EndsWith("/")) frameFolderPath += "/";
            if (!outputPath.EndsWith("/")) outputPath += "/";

            string[] frameDirs = Directory.GetFiles(frameFolderPath, "*" + FILE_EXTENSION);
            foreach (string frame in frameDirs)
            {
                string biggestDetectedFaceId;   //for now, just take the biggest face detected in the frame

                try
                {
                    biggestDetectedFaceId = await DetectBiggestFaceAsync(frame);
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n" + e.Message);
                    biggestDetectedFaceId = null;
                }

                if (biggestDetectedFaceId != null)  //if there's a face in this frame
                {
                    if (del_after_processing)
                    {
                        File.Delete(frame);
                    }

                    Dictionary<string, decimal> candidatesForFrame = await IdentifyAsync(biggestDetectedFaceId);
                    await AddNamesToNameDictAsync(candidatesForFrame);
                    ExportFrameGuesses(frame, candidatesForFrame);
                    AddFrameGuessesToAverage(candidatesForFrame);
                }
            }

            SaveAverageGuesses();
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/detect[?returnFaceId][&returnFaceLandmarks][&returnFaceAttributes]
        static async Task<string> DetectBiggestFaceAsync(string imgPath)
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

            return detectedIds[0];
        }

        static async Task<Dictionary<string, decimal>> IdentifyAsync(string faceID)
        {
            Dictionary<string, decimal> cands = new Dictionary<string, decimal>();
            
            string rsp = await IdentifyFaceAsync(faceID);
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);

            //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

            JObject faceAndCandidates = (JObject) data[0];    //single face, potentially many candidates
                
            JArray candidates = (JArray) faceAndCandidates["candidates"];
            foreach (JObject c in candidates)
            {
                cands.Add(c["personId"].Value<string>(), (decimal) c["confidence"].Value<float>());
            }

            return cands;
        }

        static async Task AddNamesToNameDictAsync(Dictionary<string, decimal> cands)
        {
            foreach (KeyValuePair<string, decimal> entry in cands)
            {
                if (!candidateNames.ContainsKey(entry.Key))
                    candidateNames.Add(entry.Key, await IdToNameAsync(entry.Key));
            }
        }

        static void ExportFrameGuesses(string framePath, Dictionary<string, decimal> cands)
        {
            string frameName = Path.GetFileName(framePath).Split('.')[0];
            string savePath = outputPath + frameName + ".txt";

            List<string> dataToSave = new List<string>();
            dataToSave.Add("{");
            for (int i = 0; i < cands.Keys.Count; i++)
            {
                string personID = cands.Keys.ElementAt(i);
                string name = candidateNames[personID];

                if ((i+1) < cands.Keys.Count)
                    dataToSave.Add("\t\"" + name + "\": " + cands[personID] + ",");
                else
                    dataToSave.Add("\t\"" + name + "\": " + cands[personID]); // last one doesn't have a comma! :P
            }
            dataToSave.Add("}");

            System.IO.File.WriteAllLines(savePath, dataToSave);
        }

        static void AddFrameGuessesToAverage(Dictionary<string, decimal> cands)
        {
            foreach (KeyValuePair<string, decimal> entry in cands)
            {
                string id = entry.Key;
                if (!averagedGuesses.ContainsKey(id))
                {
                    List<decimal> confidences = new List<decimal>();
                    confidences.Add(cands[id]);

                    averagedGuesses.Add(id, confidences);
                }
                else
                {
                    averagedGuesses[id].Add(cands[id]);
                }
            }
        }

        static void SaveAverageGuesses()
        {
            Console.WriteLine("Saving average guesses...");
            string savePath = outputPath + "averaged.txt";

            List<string> dataToSave = new List<string>();
            dataToSave.Add("{");
            for (int i = 0; i < averagedGuesses.Keys.Count; i++)
            {
                string personID = averagedGuesses.Keys.ElementAt(i);
                string name = candidateNames[personID];
                List<decimal> confidences = averagedGuesses[personID];
                
                decimal total = 0;
                foreach (decimal d in confidences)
                    total += d;
                
                int count = confidences.Count;

                decimal avg = total/count;

                if ((i+1) < averagedGuesses.Keys.Count)
                    dataToSave.Add("\t\"" + name + "\": \"" + avg + " (from " + count + " frames)" + "\",");
                else
                    dataToSave.Add("\t\"" + name + "\": \"" + avg + " (from " + count + " frames)" + "\""); // last one doesn't have a comma! :P
            }
            dataToSave.Add("}");

            System.IO.File.WriteAllLines(savePath, dataToSave);
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/identify
        static async Task<string> IdentifyFaceAsync(string faceId)
        {
            //TODO: the API is capable of handling 10 independent faces in one call of "Face - Identify"
            //currently, I am putting one face per call because it guarantees that every call will have <10 faces
            //it might be beneficial to rewrite this such that the number of API calls is minimized 
            //(and the number of faces put into each call is maximized)
            string URI = uriBase + "identify";
            string reqBody = "{\"largePersonGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + faceId + "\"]}";
            byte[] req = Encoding.UTF8.GetBytes(reqBody);

            string rsp = await MakeRequestAsync("Identify person using faceId", URI, req, "application/json", "POST");
            return rsp;
        }

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/persongroups/{personGroupId}/persons/{personId}
        static async Task<string> IdToNameAsync(string id)
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + id;
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string rsp = await MakeRequestAsync("Retrieve Person associated with ID", URI, empty, "application/json", "GET");
            JObject data = (JObject) JsonConvert.DeserializeObject(rsp);   //data should be {"personId": "...", ...}
            return data["name"].Value<string>();    //todo: make this error-proof
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

            //Console.WriteLine(">    Full URI: " + fullUri);  // debug line

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

        static string ReadJsonStrFromFile(string path, string param)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Unable to find file in path: " + path);
                return "";
            }
            string json = System.IO.File.ReadAllText(path);
            JObject data = (JObject) JsonConvert.DeserializeObject(json);
            return data[param].Value<string>();
        }
    }
}
