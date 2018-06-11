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

namespace Labelling
{
    class Program
    {
        static readonly string subscriptionKey = ReadJsonStrFromFile("../api_access_key.txt", "subscriptionKey");

        static readonly string uriBase = ReadJsonStrFromFile("../api_access_key.txt", "uriBase");
        
        const string FILE_EXTENSION = ".bmp";
        const int STARTING_INDEX = 1; // ffmpeg starts their image indexing at 1 for some reason
        const string INT_FORMAT = "D5"; //D5 for frame00001, D3 for frame001, etc
        const string DATA_FILE = "useful.txt"; // name of the file to save data into

        static string dir;

        static Dictionary<int, string> detectedFrames = new Dictionary<int, string>();

        static async Task Main(string[] args)
        {
            Console.Write("Enter the directory containing the frame images: ");
            dir = Console.ReadLine();
            if (!dir.EndsWith("/")) dir += "/";

            string[] dirs = Directory.GetFiles(dir, "*" + FILE_EXTENSION);
            int maxCount = dirs.Length;
            Console.WriteLine("There are " + maxCount + " frames in this directory...");

            //var frames = Directory.EnumerateFiles(dir, FILE_EXTENSION);
            //int firstFaceIndex = await GetFirstFaceIndexAsync(maxCount);
            //Console.WriteLine("First face found is at index " + firstFaceIndex);
            //int num = await HowManyHaveFacesAsync(maxCount);
            //Console.WriteLine("Of the " + maxCount + " frames, " + num + " have detectable faces");
            await FilterFrames(maxCount, true);
        }

        static async Task FilterFrames(int max, bool delete)
        {
            int index = STARTING_INDEX;
            while (index <= max)
            {
                // response will be a list of faces:
                // [{"faceId":"...", "faceAttributes":{"headPose": {"roll": x, "yaw": y, "pitch": 0}}}, {"faceId":"...", ...}]
                string rsp = await UploadImageGetFaceAndYawAsync(index);
                JArray faces = (JArray) JsonConvert.DeserializeObject(rsp);
                if (faces.Count > 0)
                {
                    detectedFrames.Add(index, rsp);
                    float yaw = (float) faces[0]["faceAttributes"]["headPose"]["yaw"];
                    Console.WriteLine("Yaw for frame" + index.ToString(INT_FORMAT) + ": " + yaw);
                }
                else
                {
                    //Console.WriteLine("frame" + index.ToString(INT_FORMAT) + " has no Faces in it");
                    if (delete)
                    {
                        string path = dir + "frame" + index.ToString(INT_FORMAT) + FILE_EXTENSION;
                        Console.WriteLine("Deleting frame" + index.ToString(INT_FORMAT) + ".bmp because it does not have a detectable face");
                        File.Delete(path);
                    }
                }
                index ++;
            }

            // At this point, we have a dictionary full of frame indices and responses from the server
            // Now it makes sense to save this information to a .txt file
            
            Console.WriteLine("Saving data... ");

            List<string> dataToSave = new List<string>();
            dataToSave.Add(String.Join(",", detectedFrames.Keys)); //first line: list of the useful indices
            foreach(KeyValuePair<int, string> entry in detectedFrames)
            {
                string output = "frame" + entry.Key.ToString(INT_FORMAT) + ":" + entry.Value;
                dataToSave.Add(output); //each additional line: frame#####:<json_response>
            }
            System.IO.File.WriteAllLines(dir + DATA_FILE, dataToSave);
            
            Console.WriteLine("Data saved!");
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
                if (toAdd > 1) Console.WriteLine("index #" + index + " has " + toAdd + " faces in it!");
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
            string json = System.IO.File.ReadAllText(path);
            JObject data = (JObject) JsonConvert.DeserializeObject(json);
            return data[param].Value<string>();
        }

    }
}
