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

using UnityEngine;
using UnityEngine.Networking;


namespace UnityFaceIDHelper
{
    public class FaceAPIHelper
    {
        public const string TRAINING_SUCCEEDED = "succeeded";
        public const string TRAINING_FAILED = "failed";
        public const string TRAINING_RUNNING = "running";
        public const string TRAINING_API_ERROR = "";

        readonly string subscriptionKey;
        readonly string uriBase;
        readonly string personGroupId;

        private UnityWebRequest client;

        public FaceAPIHelper(string api_access_key, string pID)
        {
            subscriptionKey = ReadJsonParamFromStr(api_access_key, "subscriptionKey");
            uriBase = ReadJsonParamFromStr(api_access_key, "uriBase");
            personGroupId = pID;
            client = new UnityWebRequest();
        }

        public async Task<Dictionary<string, decimal>> IdentifyBiggestInImageAsync(string pathToImg)
        {
            byte[] imgData = GetImageAsByteArray(pathToImg);
            return await IdentifyBiggestInImageAsync(imgData);
        }

        public async Task<Dictionary<string, decimal>> IdentifyBiggestInImageAsync(byte[] imgData)
        {
            try
            {
                List<string> faceIds = await DetectForIdentifyingAsync(imgData);

                if (faceIds.Count > 0)
                    return await IdentifyFromFaceIdAsync(faceIds[0]); // only try identifying the biggest face
                else
                    return new Dictionary<string, decimal>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        // takes in a name, outputs personId or "" if an exception occurs
        public async Task<string> CreatePersonAsync(string name, string data = "")
        {
            try
            {
                return await CreateLargePersonGroupPersonAsync(name, data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        //takes in an image, outputs the number of faces detected or -1 if an exception occurs
        public async Task<int> CountFacesAsync(byte[] imgData)
        {
            try
            {
                List<string> faceIds = await DetectForIdentifyingAsync(imgData);
                return faceIds.Count;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return -1;
            }
        }

        //takes in a personId + img, outputs a persistedFaceId or "" if an exception occurs
        public async Task<string> AddFaceAsync(string personId, byte[] imgData)
        {
            try
            {
                return await AddFaceToLargePersonGroupPersonAsync(personId, imgData);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        //takes in a personId + persistedFaceId, returns false if an exception occurs or deletes the img & returns true
        public async Task<bool> DeleteFaceAsync(string personId, string persistedFaceId)
        {
            try
            {
                return await DeleteFaceFromLargePersonGroupPersonAsync(personId, persistedFaceId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        //takes in a personId, returns the name associated with the ID, or "" if an exception occurs
        public async Task<string> GetNameFromIDAsync(string personId)
        {
            try
            {
                return await GetNameFromLargePersonGroupPersonPersonIdAsync(personId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        //returns true if api call is successful; false if an exception occurs
        public async Task<bool> StartTrainingAsync()
        {
            try
            {
                return await StartTrainingLargePersonGroupAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        //returns one of the three constants defined at the beginning of the program, or TRAINING_API_ERROR if an exception occurs
        public async Task<string> GetTrainingStatusAsync()
        {
            try
            {
                return await GetLargePersonGroupTrainingStatusAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return TRAINING_API_ERROR;
            }
        }

        private async Task<string> GetLargePersonGroupTrainingStatusAsync()
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/training";
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string trainRsp = await MakeUnityRequestAsync("Checking the status of the training", URI, empty, "application/json", "GET");
            JObject data = (JObject)JsonConvert.DeserializeObject(trainRsp);
            return data["status"].Value<string>();
        }

        private async Task<bool> StartTrainingLargePersonGroupAsync()
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/train";
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string trainRsp = await MakeUnityRequestAsync("Training the " + personGroupId + " LargePersonGroup using the added images", URI, empty, "application/json", "POST");
            return trainRsp == "";
        }

        private async Task<string> GetNameFromLargePersonGroupPersonPersonIdAsync(string personId)
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId;
            byte[] empty = Encoding.UTF8.GetBytes("{}");

            string rsp = await MakeUnityRequestAsync("Retrieve Person associated with ID", URI, empty, "application/json", "GET");
            JObject data = (JObject)JsonConvert.DeserializeObject(rsp);
            return data["name"].Value<string>();
        }

        private async Task<bool> DeleteFaceFromLargePersonGroupPersonAsync(string personId, string persistedFaceId)
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId + "/persistedFaces/" + persistedFaceId;
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            string rsp = await MakeUnityRequestAsync("Removing Image from " + personId, URI, empty, "application/octet-stream", "DELETE");
            return rsp == "";
        }

        private async Task<string> AddFaceToLargePersonGroupPersonAsync(string personId, byte[] img)
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId + "/persistedFaces?";

            string rsp = await MakeUnityRequestAsync("Adding Image to " + personId, URI, img, "application/octet-stream", "POST");

            JObject data = (JObject)JsonConvert.DeserializeObject(rsp);   //data should just be {"persistedFaceId": "..."}
            return data["persistedFaceId"].Value<string>();
        }

        private async Task<string> CreateLargePersonGroupPersonAsync(string name, string data)
        {
            string URI = uriBase + "largepersongroups/" + personGroupId + "/persons";
            byte[] encoded = Encoding.UTF8.GetBytes("{'name': '" + name + "', 'userData': '" + data + "'}");
            string rsp = await MakeUnityRequestAsync("Adding Person to Person Group", URI, encoded, "application/json", "POST");
            
            JObject returnedData = (JObject)JsonConvert.DeserializeObject(rsp);
            return returnedData["personId"].Value<string>();
        }

        private async Task<Dictionary<string, decimal>> IdentifyFromFaceIdAsync(string faceId)
        {
            string URI = uriBase + "identify";
            string reqBody = "{\"largePersonGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + faceId + "\"]}";
            byte[] req = Encoding.UTF8.GetBytes(reqBody);

            string rsp = await MakeUnityRequestAsync("Identify person using faceId", URI, req, "application/json", "POST");
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);
            //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

            JObject faceAndCandidates = (JObject) data[0];    //single face, potentially many candidates
            Dictionary<string, decimal> idsAndConfidences = new Dictionary<string, decimal>();
                
            JArray candidates = (JArray) faceAndCandidates["candidates"];
            foreach (JObject cand in candidates)
            {
                idsAndConfidences.Add(cand["personId"].Value<string>(), cand["confidence"].Value<decimal>());
            }

            return idsAndConfidences;
        }

        //just returns a list of faceIds from the detected image (note: these expire after 24 hours)
        private async Task<List<string>> DetectForIdentifyingAsync(string pathToImg)
        {
            byte[] img = GetImageAsByteArray(pathToImg);

            return await DetectForIdentifyingAsync(img);
        }

        //just returns a list of faceIds from the detected image (note: these expire after 24 hours)
        private async Task<List<string>> DetectForIdentifyingAsync(byte[] imgData)
        {
            string URI = uriBase + "detect";
            byte[] img = imgData;
            List<string> detectedIds = new List<string>();

            string rsp = await MakeUnityRequestAsync("Detect faces in an image", URI, img, "application/octet-stream", "POST");
            //response will be a list of faces: [{"faceId":"...", ...}, {"faceId":"...", ...}]
            //[{"faceId":"7be28a2d-a1b1-49bf-8c4d-078552e60fe3","faceRectangle":{"top":256,"left":616,"width":330,"height":330}}]
            JArray data = (JArray) JsonConvert.DeserializeObject(rsp);   //data should just be {"personId": "..."}
            foreach (JObject face in data)
            {
                detectedIds.Add(face["faceId"].Value<string>());
            }

            return detectedIds;
        }

        private async Task<string> MakeUnityRequestAsync(string purpose, string uri, byte[] reqBodyData, string bodyContentType, string method, Dictionary<string, string> requestParameters = null)
        {
            if (subscriptionKey == "" || uriBase == "")
            {
                throw new Exception("Please make sure that api_access_key.txt is in the correct location.");
            }

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            if (requestParameters != null)
            {
                foreach (string key in requestParameters.Keys)
                {
                    queryString[key] = requestParameters[key];
                }
            }

            var fullUri = uri + queryString;

            client = new UnityWebRequest(fullUri);
            client.SetRequestHeader("Ocp-Apim-Subscription-Key", subscriptionKey);
            client.SetRequestHeader("Content-Type", bodyContentType);
            client.downloadHandler = new DownloadHandlerBuffer();
            client.chunkedTransfer = false;

            string unityMethod = method.ToUpper();
            switch (unityMethod)
            {
                case UnityWebRequest.kHttpVerbPOST:
                case UnityWebRequest.kHttpVerbPUT:
                    client.method = unityMethod;
                    client.uploadHandler = new UploadHandlerRaw(reqBodyData);
                    await SendReq(client);
                    break;
                case UnityWebRequest.kHttpVerbGET:
                case UnityWebRequest.kHttpVerbDELETE:
                    client.method = unityMethod;
                    await SendReq(client);
                    break;
                default: throw new Exception("Unsupported method: " + unityMethod);
            }

            if (client.isNetworkError || client.isHttpError)
            {
                throw new Exception("Error: " + client.error);
            }
            else
                return client.downloadHandler.text;
            
        }

        private IEnumerator SendReq(UnityWebRequest www)
        {
            yield return www.SendWebRequest();
        }

        /*
        private async Task<string> MakeRequestAsync(string purpose, string uri, byte[] reqBodyData, string bodyContentType, string method, Dictionary<string, string> requestParameters = null)
        {
            if (subscriptionKey == "" || uriBase == "")
            {
                throw new Exception("Please make sure that api_access_key.txt is in the correct location.");
            }

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
                else if (method.ToLower() == "delete")
                {
                    response = await client.DeleteAsync(fullUri);
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
        }*/

        private byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }

        private string ReadJsonParamFromStr(string key, string param)
        {
            string json = key;
            JObject data = (JObject) JsonConvert.DeserializeObject(json);
            return data[param].Value<string>();
        }
    }
}
