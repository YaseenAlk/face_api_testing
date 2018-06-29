using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
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

        static SortedDictionary<string, Dictionary<string, decimal>> frameResults = new SortedDictionary<string, Dictionary<string, decimal>>();

        // dictionary: each key is a candidate's personId and each Value is a list of confidence vals
        static Dictionary<string, List<decimal>> averagedGuesses = new Dictionary<string, List<decimal>>();

        // key is their unique microsoft-assigned personID; value is the name associated with the personID
        static Dictionary<string, string> candidateNames = new Dictionary<string, string>();

        static long timedDuration = 0;

        const bool QUEUE_10_AT_ONCE = true;

        // Functionality:
        // Given: personGroupId, frameFolderPath, outputPath (assuming that all three already exist and are pre-processed)
        // Output: For every frame in frameFolderPath, make a .txt file (same name) with guesses + confidences
        // (and delete the frame file if del_after_processing is true)
        // Also output a file called averaged.txt that has the averaged confidence levels from every frame

        // Usage: dotnet IdentifyUnknownVideo.dll <personGroupId> <frame folder path> <output path for guess folder> <time taken so far (ms)> [-del_after_processing (optional)]
        static async Task Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: dotnet IdentifyUnknownVideo.dll <personGroupId> <frame folder path> <output path for guess folder> <time taken so far (ms)> [-del_after_processing (optional)]");
                return;
            }

            if (subscriptionKey == "" || uriBase == "")
            {
                Console.WriteLine("Please make sure that api_access_key.txt is in the correct location.");
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew(); // start timing

            if (Array.IndexOf(args, "-del_after_processing") > -1)
            {
                del_after_processing = true;
            }

            // todo: sanitize these inputs later? going to assume they're all correct for now...
            personGroupId = args[0].ToLower();  //force lowercase, just in case!
            frameFolderPath = args[1];
            outputPath = args[2];
            timedDuration += Int32.Parse(args[3]);
            
            if (!frameFolderPath.EndsWith("/")) frameFolderPath += "/";
            if (!outputPath.EndsWith("/")) outputPath += "/";

            string[] frameDirs = Directory.GetFiles(frameFolderPath, "*" + FILE_EXTENSION);
            Dictionary<string, string> faceIdToFileName = new Dictionary<string, string>();

            if (QUEUE_10_AT_ONCE)
            {
                int count = frameDirs.Length;
                List<string> frameQueue = new List<string>();
                while (count > 0)
                {
                    string frame = frameDirs[count - 1];

                    string biggestDetectedFaceId;

                    try
                    {
                        biggestDetectedFaceId = await DetectBiggestFaceAsync(frame);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("\n" + e.Message);
                        biggestDetectedFaceId = null;
                    }

                    count--;

                    if (biggestDetectedFaceId != null)
                    {
                        if (del_after_processing)
                        {
                            File.Delete(frame);
                        }

                        frameQueue.Add(biggestDetectedFaceId);
                        string frameName = Path.GetFileName(frame).Split('.')[0];
                        faceIdToFileName.Add(biggestDetectedFaceId, frameName);
                    }

                    if (frameQueue.Count == 10 || count == 0)
                    {
                        Dictionary<string, Dictionary<string, decimal>> identified = await IdentifyMultipleFaceAsync(frameQueue.ToArray());

                        foreach (KeyValuePair<string, Dictionary<string, decimal>> entry in identified)
                        {
                            await AddNamesToNameDictAsync(entry.Value);
                            frameResults.Add(faceIdToFileName[entry.Key], entry.Value);
                        }

                        frameQueue = new List<string>();
                    }
                }
            }
            else
            {
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

                        Dictionary<string, decimal> candidatesForFrame = await IdentifySingleFaceAsync(biggestDetectedFaceId);
                        await AddNamesToNameDictAsync(candidatesForFrame);
                        string frameName = Path.GetFileName(frame).Split('.')[0];
                        frameResults.Add(frameName, candidatesForFrame);
                    }
                }
            }

            foreach (KeyValuePair<string, Dictionary<string, decimal>> frameRes in frameResults)
            {
                AddFrameResultToAverage(frameRes.Value);
            }

            watch.Stop();   //stop timing
            var elapsed = watch.ElapsedMilliseconds;
            timedDuration += elapsed;

            Console.WriteLine("Generating Json...");

            watch.Reset();
            watch.Start();
            GenerateJSONFile();
            watch.Stop();

            Console.WriteLine("Generated Json file in " + watch.ElapsedMilliseconds + "ms");
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

        static async Task<Dictionary<string, Dictionary<string, decimal>>> IdentifyMultipleFaceAsync(string[] faceIds)
        {
            string rsp = await IdentifyFacesAsync(faceIds);
            JArray faces = (JArray)JsonConvert.DeserializeObject(rsp);

            //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

            Dictionary<string, Dictionary<string, decimal>> idsAndCandidates = new Dictionary<string, Dictionary<string, decimal>>();
            foreach (JObject faceAndCandidates in faces)
            {
                Dictionary<string, decimal> cands = new Dictionary<string, decimal>();

                JArray candidates = (JArray) faceAndCandidates["candidates"];
                foreach (JObject c in candidates)
                {
                    cands.Add(c["personId"].Value<string>(), c["confidence"].Value<decimal>());
                }
                idsAndCandidates.Add(faceAndCandidates["faceId"].Value<string>(), cands);
            }
            return idsAndCandidates;
        }

        static async Task<Dictionary<string, decimal>> IdentifySingleFaceAsync(string faceID)
        {
            Dictionary<string, decimal> cands = new Dictionary<string, decimal>();

            List<string> faceList = new List<string>();
            faceList.Add(faceID);

            string rsp = await IdentifyFacesAsync(faceList.ToArray());
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);

            //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

            JObject faceAndCandidates = (JObject) data[0];    //single face, potentially many candidates
                
            JArray candidates = (JArray) faceAndCandidates["candidates"];
            foreach (JObject c in candidates)
            {
                cands.Add(c["personId"].Value<string>(), c["confidence"].Value<decimal>());
            }

            return cands;
        }

        static void AddFrameResultToAverage(Dictionary<string, decimal> cands)
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

        // Goal: https://[location].api.cognitive.microsoft.com/face/v1.0/identify
        static async Task<string> IdentifyFacesAsync(string[] faceIds)
        {
            //TODO: the API is capable of handling 10 independent faces in one call of "Face - Identify"
            //currently, I am putting one face per call because it guarantees that every call will have <10 faces
            //it might be beneficial to rewrite this such that the number of API calls is minimized 
            //(and the number of faces put into each call is maximized)
            string URI = uriBase + "identify";
            string reqBody = "{\"largePersonGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + String.Join("\",\"", faceIds) + "\"]}";
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

        // in the form of [name, # frames with this guess, weighted confidence]
        // note: "name" here is the microsoft-assigned personID (guaranteed unique),
        // NOT the PRG id (likely unique but not guaranteed for every experiment)
        static Tuple<string, int, decimal>[] GenerateOverallCandidates()
        {
            List<Tuple<string, int, decimal>> overallCands = new List<Tuple<string, int, decimal>>();

            foreach (KeyValuePair<string, List<decimal>> candidate in averagedGuesses)
            {
                List<decimal> confidences = candidate.Value;

                string name = candidate.Key;
                int count = confidences.Count;
                // Using the formula that Hae Won gave me:
                // pid_confidence = {(sum of all pid confidence)/(# of pid frames)} * {(# of pid frames)/(total# of frames)}
                decimal weightedVal = confidences.Sum() / count * count / frameResults.Count;

                Tuple<string, int, decimal> overall = new Tuple<string, int, decimal>(name, count, weightedVal);
                overallCands.Add(overall);
            }

            return overallCands.ToArray();
        }

        static string FinalRecommendation(Tuple<string, int, decimal>[] overallCands)
        {
            decimal largestConfidence = 0;
            int largestIndex = -1;

            for (int i = 0; i < overallCands.Length; i++)
            {
                Tuple<string, int, decimal> candidate = overallCands[i];
                decimal conf = candidate.Item3;
                if (conf >= largestConfidence)
                {
                    largestConfidence = conf;
                    largestIndex = i;
                }
            }

            if (largestIndex == -1) //empty list..?
                return "";
            else
                return overallCands[largestIndex].Item1;
        }

        // converts a string from the Microsoft-assigned ID to the PRG-assigned ID

        // unfortunately, the JSON output of the program doesn't currently 
        // support a way for 2 LargePersonGroup Persons to share a name 
        // (because JsonConvert.ToString uses instance data names to convert to JSON)
        static string MicrosoftIDToPRGID(string mID)
        {
            return candidateNames[mID];
        }

        static async Task AddNamesToNameDictAsync(Dictionary<string, decimal> cands)
        {
            foreach (KeyValuePair<string, decimal> entry in cands)
            {
                if (!candidateNames.ContainsKey(entry.Key))
                    candidateNames.Add(entry.Key, await IdToNameAsync(entry.Key));
            }
        }

        //convert from Microsoft ID to PRG ID
        static Object[][] OverallCandidatesDisplay(Tuple<string, int, decimal>[] overallCands)
        {
            List<Object[]> renamed = new List<Object[]>();

            foreach (Tuple<string, int, decimal> cand in overallCands)
            {
                string newName = MicrosoftIDToPRGID(cand.Item1);
                Object[] newTuple = { newName, cand.Item2, cand.Item3 };
                renamed.Add(newTuple);
            }

            return renamed.ToArray();
        }

        // converts from Microsoft ID to PRG ID
        static Dictionary<string, decimal> GuessesDisplay(Dictionary<string, decimal> confidences)
        {
            Dictionary<string, decimal> converted = new Dictionary<string, decimal>();

            foreach (KeyValuePair<string, decimal> entry in confidences)
            {
                string newName = MicrosoftIDToPRGID(entry.Key);
                converted.Add(newName, entry.Value);
            }

            return converted;
        }

        // Everything below is json output code!
        // DynamicObject code for RootJsonObj and FrameObj are adapted from https://stackoverflow.com/a/37997635/4036588

        static void GenerateJSONFile()
        {
            RootJsonObj jsonObj = new RootJsonObj();

            Dictionary<string, FrameObj> frameObjs = GenerateFrameObjs();
            jsonObj.FrameObjects = frameObjs;

            ResultObj resultObj = new ResultObj();
            resultObj.ValidFrames = frameResults.Count;
            Tuple<string, int, decimal>[] overallCandidates = GenerateOverallCandidates();
            resultObj.Candidates = OverallCandidatesDisplay(overallCandidates);
            string finalRec = FinalRecommendation(overallCandidates);
            if (finalRec == "")
                resultObj.FinalRec = "none";
            else
                resultObj.FinalRec = MicrosoftIDToPRGID(FinalRecommendation(overallCandidates));
            jsonObj.Result = resultObj;

            jsonObj.TimeTaken = timedDuration;

            string savePath = outputPath + "guesses.json";

            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(savePath))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, jsonObj);
            }
        }

        static Dictionary<string, FrameObj> GenerateFrameObjs()
        {
            Dictionary<string, FrameObj> objs = new Dictionary<string, FrameObj>();
            foreach (KeyValuePair<string, Dictionary<string, decimal>> entry in frameResults)
            {
                FrameObj frame = new FrameObj(entry.Key);
                frame.Confidences = GuessesDisplay(entry.Value);
                objs.Add(frame.GetFrameName(), frame);
            }
            return objs;
        }

        class RootJsonObj : DynamicObject
        {
            public Dictionary<string, FrameObj> FrameObjects { get; set; }
            [JsonProperty(PropertyName = "result")]
            public ResultObj Result { get; set; }
            [JsonProperty(PropertyName = "time_taken_ms")]
            public long TimeTaken { get; set; }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                foreach (KeyValuePair<string, FrameObj> entry in FrameObjects)
                {
                    yield return entry.Key;
                }

                foreach (var prop in GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != nameof(FrameObjects)))
                {
                    yield return prop.Name;
                }
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (FrameObjects.ContainsKey(binder.Name))
                {
                    result = FrameObjects[binder.Name];
                    return true;
                }

                return base.TryGetMember(binder, out result);
            }
        }

        class ResultObj
        {
            [JsonProperty(PropertyName = "valid_frames")]
            public int ValidFrames { get; set; }
            [JsonProperty(PropertyName = "candidates")]
            public Object[][] Candidates { get; set; }
            [JsonProperty(PropertyName = "final_recommendation")]
            public string FinalRec { get; set; }
        }

        class FrameObj : DynamicObject
        {
            private string _name;

            public FrameObj(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                _name = name;
            }

            public string GetFrameName()
            {
                return _name;
            }

            public Dictionary<string, decimal> Confidences { get; set; }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                foreach (KeyValuePair<string, decimal> entry in Confidences)
                {
                    yield return entry.Key;
                }
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (Confidences.ContainsKey(binder.Name))
                {
                    result = Confidences[binder.Name];
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
        }

    }
}
