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

namespace Filtering
{
    class Program
    {
        // for this version, just put api_access_key.txt in the same 
        static readonly string subscriptionKey = ReadJsonStrFromFile("api_access_key.txt", "subscriptionKey");
        static readonly string uriBase = ReadJsonStrFromFile("api_access_key.txt", "uriBase");

        //TODO: maybe convert these constants into command line arguments?        
        const string FILE_EXTENSION = ".bmp";
        const int STARTING_INDEX = 1; // ffmpeg starts their image indexing at 1 for some reason
        const string INT_FORMAT = "D5"; //D5 for frame00001, D3 for frame001, etc

        const string USEFUL_DATA_FILE = "useful.txt"; // name of the file to save useful frames into
        const string LIBRARY_DATA_FILE = "library.txt"; // name of the file to save library-worthy frames into
        
        
        const decimal MIN_YAW_DIFF = 5.0m; //units are in degrees, and range is [-90, 90]
        const int FRAMES_PER_YAW_VAL = 2; //number of frames to maintain for a small yaw value range dictated by MIN_YAW_DIFF
        static Dictionary<decimal, int> libraryCount;

        static string dir;

        static Dictionary<int, string> detectedFrames;
        static Dictionary<int, string> libraryFrames;

        static bool no_output = false;
        static bool justFilterDetectable = false;
        static bool reFilterUsingTxt = false;

        // Usage: dotnet FilteringTerminalArgs.dll <frame img dir> [-no_output (optional)] [-just_filter_detectables (optional)] [-refilter_using_txt (optional)]
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: dotnet FilteringTerminalArgs.dll <frame img dir> [-no_output (optional)] [-just_filter_detectables (optional)] [-refilter_using_txt (optional)]");
                return;
            }

            if (subscriptionKey == "" || uriBase == "")
            {
                Console.WriteLine("Please make sure that api_access_key.txt is in the correct location.");
                return;
            }

            if (Array.IndexOf(args, "-no_output") > -1)
            {
                no_output = true;
            }

            if (Array.IndexOf(args, "-just_filter_detectables") > -1)
            {
                justFilterDetectable = true;
            }

            if (Array.IndexOf(args, "-refilter_using_txt") > -1)
            {
                reFilterUsingTxt = true;
            }

            InitializeInstanceData();
            dir = args[0];
            if (!dir.EndsWith("/")) dir += "/";

            string[] dirs = Directory.GetFiles(dir, "*" + FILE_EXTENSION);
            int maxCount = dirs.Length;
            if (!no_output) Console.WriteLine("There are " + maxCount + " frames in this directory...");

            //var frames = Directory.EnumerateFiles(dir, FILE_EXTENSION);
            //int firstFaceIndex = await GetFirstFaceIndexAsync(maxCount);
            //Console.WriteLine("First face found is at index " + firstFaceIndex);
            //int num = await HowManyHaveFacesAsync(maxCount);
            //Console.WriteLine("Of the " + maxCount + " frames, " + num + " have detectable faces");
            if (!File.Exists(dir + USEFUL_DATA_FILE))
            {
                if (reFilterUsingTxt)
                {
                    Console.WriteLine("Cannot re-filter using " + USEFUL_DATA_FILE + " because it does not exist in this directory.");
                    return;
                }
                await FilterDetectableFacesAsync(maxCount, true); //for non-detectable faces
            }
            else if (reFilterUsingTxt)
            {
                CleanUpUsingTxtFile(dirs);
            }
            else if (justFilterDetectable)
            {
                Console.WriteLine("It seems that detectable faces have already been filtered in " + dir);
                return;
            }
            else
                LoadDetectableFaceList();
            
            if (!justFilterDetectable)
            {
                PrepareToFilterLibrary();
                FilterLibraryFaces();   //once we have a datafile with yaw values saved, this should only need to happen locally (and should be very fast)
            }
        }

        static void InitializeInstanceData()
        {
            detectedFrames = new Dictionary<int, string>();
            libraryFrames = new Dictionary<int, string>();
            libraryCount = new Dictionary<decimal, int>();
        }

        // generates a dictionary of rounded range values
        // each key represents a range, and each value represents the number of frames with yaw vals in that range
        // so for the key 0, the range is -MIN_YAW_DIFF to MIN_YAW_DIFF;
        // for the key 89, the range is 89-MIN_YAW_DIFF to 89+MIN_YAW_DIFF
        static void PrepareToFilterLibrary()
        {
            if (!no_output) Console.WriteLine("Generating dictionary for library...");
            for (decimal d = -90m; d <= 90m; d += MIN_YAW_DIFF*2)
            {
                libraryCount.Add(d, 0);
            }
            if (!no_output) Console.WriteLine("Done generating dictionary!");
        }

        static void FilterLibraryFaces()
        {
            if (!no_output) Console.WriteLine("Filtering Faces to use for the library...");

            if (Directory.Exists(dir + "training/"))
            {
                if (!no_output) Console.WriteLine("The training directory seems to already exist. I'm going to assume that you would like to regenerate the library, so I will first clear the existing training folder.");
                System.IO.DirectoryInfo di = new DirectoryInfo(dir + "training/");
                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete(); 
                }
                if (!no_output) Console.WriteLine("\"training\" directory cleared.");
            }
            else
            {
                Directory.CreateDirectory(dir + "training/");
            }

            foreach (KeyValuePair<int, string> frame in detectedFrames)
            {
                int frameNumber = frame.Key;
                string frameData = frame.Value;
                if (IsWorthAdding(frameData))
                {
                    string fromPath = dir + "frame" + frameNumber.ToString(INT_FORMAT) + FILE_EXTENSION;
                    string toDir = dir + "training/";
                    File.Copy(fromPath, toDir, true);
                    libraryFrames.Add(frameNumber, frameData);  //not sure if there's a more efficient way to do this
                    UpdateLibraryCount(frameData);
                }
            }
            if (!no_output) Console.WriteLine("Done filtering library Faces!");

            // save training data in a separate txt file
            string dataSavePath = dir + LIBRARY_DATA_FILE;
            ExportFrameData(libraryFrames, dataSavePath);
        }

        static bool IsWorthAdding(string data)
        {
            // to round x to the nearest n (also works for decimals!):
            // Math.Round(x / n) * n

            float yaw = GetYawFromFaceListJSON(data);
            decimal n = MIN_YAW_DIFF*2;
            decimal roundedRangeVal = Math.Round((decimal) yaw / n) * n;
            return libraryCount[roundedRangeVal] < FRAMES_PER_YAW_VAL;
        }

        static void UpdateLibraryCount(string data)
        {
            float yaw = GetYawFromFaceListJSON(data);
            decimal n = MIN_YAW_DIFF*2;
            decimal roundedRangeVal = Math.Round((decimal) yaw / n) * n;
            libraryCount[roundedRangeVal]++;
        }

        // Note: This always returns the yaw val for the FIRST Face in the FaceList
        // This isn't usually relevant (because almost all frames will only have 1 face)
        // but occasionally, some frames may have other people in them
        // In which case, the first Face in the FaceList is always the biggest Face in the frame
        static float GetYawFromFaceListJSON(string json)
        {
            // Assuming json is a string in the form
            // [{"faceId":"...", ..., "faceAttributes":{"headPose": {"roll": x, "yaw": y, "pitch": 0}}}, {"faceId":"...", ...}]
            JArray faces = (JArray) JsonConvert.DeserializeObject(json);
            return (float) faces[0]["faceAttributes"]["headPose"]["yaw"];
        }

        static void LoadDetectableFaceList()
        {
            if (!no_output) Console.WriteLine("Loading useful FaceList...");
            string[] lines = System.IO.File.ReadAllLines(dir + USEFUL_DATA_FILE);
            int[] usefulFrames = Array.ConvertAll(lines[0].Split(","), int.Parse);
            for (int i = 1; i < lines.Length; i++)
            {
                // a line might look like this
                // "frame00000:[{"faceId":"...","faceRectangle":...,"faceAttributes":{"headPose":{"pitch":0.0,"roll":x,"yaw":y}}}]"
                // we need to skip the string "frame00000:" to get the JSON response
                int padding = Int32.Parse(INT_FORMAT.Substring(1));
                int skip = "frame".Length + padding + 1;    //+1 for the colon
                detectedFrames.Add(usefulFrames[i-1], lines[i].Substring(skip));
            }
            if (!no_output) Console.WriteLine("List loaded.");
        }

        // this is to filter non-detectable faces out
        static async Task FilterDetectableFacesAsync(int max, bool delete)
        {
            if (!no_output) Console.WriteLine("Filtering frames with detectable Faces...");
            int index = STARTING_INDEX;
            while (index <= max)
            {
                // response will be a list of faces:
                // [{"faceId":"...", ..., "faceAttributes":{"headPose": {"roll": x, "yaw": y, "pitch": 0}}}, {"faceId":"...", ...}]
                string rsp = await UploadImageGetFaceAndYawAsync(index);
                try 
                {
                    JArray faces = (JArray) JsonConvert.DeserializeObject(rsp);
                    if (faces.Count > 0)
                    {
                        detectedFrames.Add(index, rsp);
                        float yaw = (float) faces[0]["faceAttributes"]["headPose"]["yaw"];
                        if (!no_output) Console.WriteLine("Yaw for frame" + index.ToString(INT_FORMAT) + ": " + yaw);
                    }
                    else
                    {
                        //Console.WriteLine("frame" + index.ToString(INT_FORMAT) + " has no Faces in it");
                        if (delete)
                        {
                            string path = dir + "frame" + index.ToString(INT_FORMAT) + FILE_EXTENSION;
                            if (!no_output) Console.WriteLine("Deleting frame" + index.ToString(INT_FORMAT) + ".bmp because it does not have a detectable face");
                            File.Delete(path);
                        }
                    }
                    index ++;
                } 
                catch (Exception e)
                {
                    Console.WriteLine("Error occurred when trying to filter face #" + index);
                    Console.WriteLine("Exception: " + e.ToString());
                    Console.WriteLine("HTTP Response: " + rsp);
                }
                
            }
            if (!no_output) Console.WriteLine("Done filtering detectable faces!");

            // At this point, we have a dictionary full of frame indices and responses from the server
            // Now it makes sense to save this information to a .txt file
            string dataSavePath = dir + USEFUL_DATA_FILE;
            ExportFrameData(detectedFrames, dataSavePath);
        }

        static void ExportFrameData(Dictionary<int, string> frameList, string pathToSave)
        {
            if (!no_output) Console.WriteLine("Saving data... ");

            List<string> dataToSave = new List<string>();
            dataToSave.Add(String.Join(",", frameList.Keys)); //first line: list of the useful indices
            foreach(KeyValuePair<int, string> entry in frameList)
            {
                string output = "frame" + entry.Key.ToString(INT_FORMAT) + ":" + entry.Value;
                dataToSave.Add(output); //each additional line: frame#####:<json_response>
            }
            System.IO.File.WriteAllLines(pathToSave, dataToSave);
            
            if (!no_output) Console.WriteLine("Data saved!");
        }

        static void CleanUpUsingTxtFile(string[] dirs)
        {
            LoadDetectableFaceList();
            List<string> detectableFileNames = new List<string>();
            foreach (KeyValuePair<int, string> entry in detectedFrames)
                detectableFileNames.Add("frame" + entry.Key.ToString(INT_FORMAT) + FILE_EXTENSION.ToLower());
            
            foreach (string fileDir in dirs)
            {
                string ext = Path.GetExtension(fileDir);

                if (ext.ToLower() == FILE_EXTENSION.ToLower())
                {
                    string fileName = Path.GetFileName(fileDir).ToLower();
                
                    if (!detectableFileNames.Contains(fileName))
                    {
                        File.Delete(fileDir);
                    }
                }
            }
        }

        static async Task<string> UploadImageGetFaceAndYawAsync(int picNum)
        {
            string filePath = dir + "frame" + picNum.ToString(INT_FORMAT) + FILE_EXTENSION;   //ToString(INT_FORMAT) adds leading 0s

            string URI = uriBase + "detect?";

            byte[] img = GetImageAsByteArray(filePath);
            Dictionary<string, string> requestParam = new Dictionary<string, string>();
            requestParam.Add("returnFaceAttributes", "headPose");

            string rsp = await MakeRequestAsync("Detect Face + Store Yaw value " + picNum.ToString(), URI, img, "application/octet-stream", "POST", requestParam);
            return rsp;
        }

        static async Task<int> HowManyHaveFacesAsync(int max)
        {
            int count = 0;
            int index = STARTING_INDEX;
            while (index <= max)
            {
                int toAdd = await NumFacesInPicAsync(index);
                if (toAdd > 1) if (!no_output) Console.WriteLine("index #" + index + " has " + toAdd + " faces in it!");
                count += (toAdd > 0) ? 1 : 0;
                //Console.WriteLine("Current ratio: " + count + " / " + index);
                index ++;
            }
            return count;
        }

        static async Task<int> GetFirstFaceIndexAsync(int max)
        {
            int index = STARTING_INDEX;
            while (index <= max)
            {
                int faces = await NumFacesInPicAsync(index);
                if (faces > 0) break;
                index++;
            }
            return index;
        }

        // Request URI: https://[location].api.cognitive.microsoft.com/face/v1.0/detect[?returnFaceId][&returnFaceLandmarks][&returnFaceAttributes]
        static async Task<int> NumFacesInPicAsync(int picNum)
        {
            string filePath = dir + "frame" + picNum.ToString(INT_FORMAT) + FILE_EXTENSION;
            
            string URI = uriBase + "detect";

            byte[] img = GetImageAsByteArray(filePath);

            string rsp = await MakeRequestAsync("Count number of faces in frame " + picNum.ToString(), URI, img, "application/octet-stream", "POST");
            //response will be a list of faces: [{"faceId":"...", ...}, {"faceId":"...", ...}]
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);
            return data.Count;
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
