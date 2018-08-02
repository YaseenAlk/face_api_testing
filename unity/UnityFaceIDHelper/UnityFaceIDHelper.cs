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
using UnityFaceIDHelper;

using Messages.face_msgs;

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

        //takes in an image, outputs the number of faces detected or -1 if an exception occurs
        public FaceAPICall<int> CountFacesCall(byte[] imgData)
        {
            FaceAPICall<int> call = new FaceAPICall<int>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.FACE_DETECT,
                    content_type = FaceAPIRequest.CONTENT_STREAM,
                    request_parameters = "",
                    request_body = imgData
                },
                apiCall = DetectForIdentifyingRspAsync(imgData),
                processResponse = CountFacesProcessRsp(),
                defaultResult = -1
            };
            return call;
        }

        // slight modification of DetectForIdentifyingRspProcessRsp()
        private Func<FaceAPIResponse, int> CountFacesProcessRsp()
        {
            return (FaceAPIResponse rsp) =>
            {
                List<string> detectedIds = new List<string>();

                string jsonRsp = rsp.response;

                JArray data = (JArray)JsonConvert.DeserializeObject(jsonRsp);   //data should just be {"personId": "..."}
                foreach (JObject face in data)
                {
                    detectedIds.Add(face["faceId"].Value<string>());
                }

                return detectedIds.Count;
            };
        }

        //returns one of the three constants defined at the beginning of the program, or TRAINING_API_ERROR if an exception occurs
        public FaceAPICall<string> GetLargePersonGroupTrainingStatusCall()
        {
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            FaceAPICall<string> call = new FaceAPICall<string>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_GET, 
                    request_type = FaceAPIRequest.LARGEPERSONGROUP_GETTRAININGSTATUS, 
                    content_type = FaceAPIRequest.CONTENT_JSON, 
                    request_parameters = "", 
                    request_body = empty
                },
                apiCall = GetLargePersonGroupTrainingStatusRspAsync(),
                processResponse = GetLargePersonGroupTrainingStatusProcessRsp(),
                defaultResult = TRAINING_API_ERROR
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> GetLargePersonGroupTrainingStatusRspAsync()
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/training";
                byte[] empty = Encoding.UTF8.GetBytes("{}");
                FaceAPIResponse rsp = await MakeUnityRequestAsync("Checking the status of the training", URI, empty, "application/json", "GET");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, string> GetLargePersonGroupTrainingStatusProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                JObject data = (JObject)JsonConvert.DeserializeObject(jsonRsp);

                return data["status"].Value<string>();
            };
        }

        //returns true if api call is successful; false if an exception occurs
        public FaceAPICall<bool> StartTrainingLargePersonGroupCall()
        {
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            FaceAPICall<bool> call = new FaceAPICall<bool>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.LARGEPERSONGROUP_TRAIN,
                    content_type = FaceAPIRequest.CONTENT_JSON,
                    request_parameters = "",
                    request_body = empty
                },
                apiCall = StartTrainingLargePersonGroupRspAsync(),
                processResponse = StartTrainingLargePersonGroupProcessRsp(),
                defaultResult = false
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> StartTrainingLargePersonGroupRspAsync()
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/train";
                byte[] empty = Encoding.UTF8.GetBytes("{}");
                FaceAPIResponse rsp = await MakeUnityRequestAsync("Training the " + personGroupId + " LargePersonGroup using the added images", URI, empty, "application/json", "POST");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, bool> StartTrainingLargePersonGroupProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                return jsonRsp == "";
            };
        }

        //takes in a personId, returns the name associated with the ID, or "" if an exception occurs
        public FaceAPICall<string> GetNameFromLargePersonGroupPersonPersonIdCall(string personId)
        {
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            FaceAPICall<string> call = new FaceAPICall<string>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_GET,
                    request_type = FaceAPIRequest.LARGEPERSONGROUPPERSON_GET,
                    content_type = FaceAPIRequest.CONTENT_JSON,
                    request_parameters = "",
                    request_body = empty,
                },
                apiCall = GetNameFromLargePersonGroupPersonPersonIdRspAsync(personId),
                processResponse = GetNameFromLargePersonGroupPersonPersonIdProcessRsp(),
                defaultResult = ""
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> GetNameFromLargePersonGroupPersonPersonIdRspAsync(string personId)
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId;
                byte[] empty = Encoding.UTF8.GetBytes("{}");

                FaceAPIResponse rsp = await MakeUnityRequestAsync("Retrieve Person associated with ID", URI, empty, "application/json", "GET");
                return rsp;
            };
        }

        private Func<FaceAPIResponse, string> GetNameFromLargePersonGroupPersonPersonIdProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                JObject data = (JObject)JsonConvert.DeserializeObject(jsonRsp);

                return data["name"].Value<string>();
            };
        }

        //takes in a personId + persistedFaceId, returns false if an exception occurs or deletes the img & returns true
        public FaceAPICall<bool> DeleteFaceFromLargePersonGroupPersonCall(string personId, string persistedFaceId)
        {
            byte[] empty = Encoding.UTF8.GetBytes("{}");
            FaceAPICall<bool> call = new FaceAPICall<bool>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_DELETE,
                    request_type = FaceAPIRequest.LARGEPERSONGROUPPERSON_DELETEFACE,
                    content_type = FaceAPIRequest.CONTENT_JSON,
                    request_parameters = "",
                    request_body = empty
                },
                apiCall = DeleteFaceFromLargePersonGroupPersonRspAsync(personId, persistedFaceId),
                processResponse = DeleteFaceFromLargePersonGroupPersonProcessRsp(),
                defaultResult = false
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> DeleteFaceFromLargePersonGroupPersonRspAsync(string personId, string persistedFaceId)
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId + "/persistedFaces/" + persistedFaceId;
                byte[] empty = Encoding.UTF8.GetBytes("{}");
                FaceAPIResponse rsp = await MakeUnityRequestAsync("Removing Image from " + personId, URI, empty, "application/json", "DELETE");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, bool> DeleteFaceFromLargePersonGroupPersonProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                return jsonRsp == "";
            };
        }

        //takes in a personId + img, outputs a persistedFaceId or "" if an exception occurs
        public FaceAPICall<string> AddFaceToLargePersonGroupPersonCall(string personId, byte[] img)
        {
            FaceAPICall<string> call = new FaceAPICall<string>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.LARGEPERSONGROUPPERSON_ADDFACE,
                    content_type = FaceAPIRequest.CONTENT_STREAM,
                    request_parameters = "",
                    request_body = img
                },
                apiCall = AddFaceToLargePersonGroupPersonRspAsync(personId, img),
                processResponse = AddFaceToLargePersonGroupPersonProcessRsp(),
                defaultResult = ""
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> AddFaceToLargePersonGroupPersonRspAsync(string personId, byte[] img)
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/persons/" + personId + "/persistedFaces?";

                FaceAPIResponse rsp = await MakeUnityRequestAsync("Adding Image to " + personId, URI, img, "application/octet-stream", "POST");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, string> AddFaceToLargePersonGroupPersonProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                JObject data = (JObject)JsonConvert.DeserializeObject(jsonRsp);   //data should just be {"persistedFaceId": "..."}

                return data["persistedFaceId"].Value<string>();
            };
        }

        // takes in a name, outputs personId or "" if an exception occurs
        public FaceAPICall<string> CreateLargePersonGroupPersonCall(string name, string data = "")
        {
            string reqBody = "{'name': '" + name + "', 'userData': '" + data + "'}";
            byte[] req = Encoding.UTF8.GetBytes(reqBody);

            FaceAPICall<string> call = new FaceAPICall<string>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.LARGEPERSONGROUPPERSON_CREATE,
                    content_type = FaceAPIRequest.CONTENT_JSON,
                    request_parameters = "",
                    request_body = req
                },
                apiCall = CreateLargePersonGroupPersonRspAsync(name, data),
                processResponse = CreateLargePersonGroupPersonProcessRsp(),
                defaultResult = ""
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> CreateLargePersonGroupPersonRspAsync(string name, string data)
        {
            return async () => {
                string URI = uriBase + "largepersongroups/" + personGroupId + "/persons";
                byte[] encoded = Encoding.UTF8.GetBytes("{'name': '" + name + "', 'userData': '" + data + "'}");
                FaceAPIResponse rsp = await MakeUnityRequestAsync("Adding Person to Person Group", URI, encoded, "application/json", "POST");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, string> CreateLargePersonGroupPersonProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                JObject returnedData = (JObject)JsonConvert.DeserializeObject(jsonRsp);

                return returnedData["personId"].Value<string>();
            };
        }

        public FaceAPICall<Dictionary<string, decimal>> IdentifyFromFaceIdCall(string faceId)
        {
            string reqBody = "{\"largePersonGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + faceId + "\"]}";
            byte[] req = Encoding.UTF8.GetBytes(reqBody);

            return new FaceAPICall<Dictionary<string, decimal>>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.FACE_IDENTIFY,
                    content_type = FaceAPIRequest.CONTENT_JSON,
                    request_parameters = "",
                    request_body = req
                },
                apiCall = IdentifyFromFaceIdRspAsync(faceId),
                processResponse = IdentifyFromFaceIdProcessRsp(),
                defaultResult = null
            };
        }

        private Func<Task<FaceAPIResponse>> IdentifyFromFaceIdRspAsync(string faceId)
        {
            return async () => {
                string URI = uriBase + "identify";
                string reqBody = "{\"largePersonGroupId\": \"" + personGroupId + "\", \"faceIds\": [\"" + faceId + "\"]}";
                byte[] req = Encoding.UTF8.GetBytes(reqBody);

                FaceAPIResponse rsp = await MakeUnityRequestAsync("Identify person using faceId", URI, req, "application/json", "POST");

                return rsp;
            };
        }

        private Func<FaceAPIResponse, Dictionary<string, decimal>> IdentifyFromFaceIdProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                string jsonRsp = rsp.response;

                JArray data = (JArray)JsonConvert.DeserializeObject(jsonRsp);
                //data should be {[{"faceId":"...", "candidates": [{"personId":"...", "confidence": #.##}, {"personId":"...", "confidence": #.##}] }]}

                JObject faceAndCandidates = (JObject)data[0];    //single face, potentially many candidates
                Dictionary<string, decimal> idsAndConfidences = new Dictionary<string, decimal>();

                JArray candidates = (JArray)faceAndCandidates["candidates"];
                foreach (JObject cand in candidates)
                {
                    idsAndConfidences.Add(cand["personId"].Value<string>(), cand["confidence"].Value<decimal>());
                }

                return idsAndConfidences;
            };
        }

        //just returns a list of faceIds from the detected image (note: these expire after 24 hours)
        public FaceAPICall<List<string>> DetectForIdentifyingCall(string pathToImg)
        {
            byte[] img = GetImageAsByteArray(pathToImg);

            return DetectForIdentifyingCall(img);
        }

        //just returns a list of faceIds from the detected image (note: these expire after 24 hours)
        public FaceAPICall<List<string>> DetectForIdentifyingCall(byte[] imgData)
        {
            FaceAPICall<List<string>> call = new FaceAPICall<List<string>>
            {
                request = new FaceAPIRequest
                {
                    request_method = FaceAPIRequest.HTTP_POST,
                    request_type = FaceAPIRequest.FACE_DETECT,
                    content_type = FaceAPIRequest.CONTENT_STREAM,
                    request_parameters = "",
                    request_body = imgData
                },
                apiCall = DetectForIdentifyingRspAsync(imgData),
                processResponse = DetectForIdentifyingProcessRsp(),
                defaultResult = null
            };
            return call;
        }

        private Func<Task<FaceAPIResponse>> DetectForIdentifyingRspAsync(byte[] imgData)
        {
            return async () => {
                string URI = uriBase + "detect";
                byte[] img = imgData;

                FaceAPIResponse rsp = await MakeUnityRequestAsync("Detect faces in an image", URI, img, "application/octet-stream", "POST");
                return rsp;
            };
        }

        private Func<FaceAPIResponse, List<string>> DetectForIdentifyingProcessRsp()
        {
            return (FaceAPIResponse rsp) => {
                List<string> detectedIds = new List<string>();

                string jsonRsp = rsp.response;

                //response will be a list of faces: [{"faceId":"...", ...}, {"faceId":"...", ...}]
                //[{"faceId":"7be28a2d-a1b1-49bf-8c4d-078552e60fe3","faceRectangle":{"top":256,"left":616,"width":330,"height":330}}]
                JArray data = (JArray)JsonConvert.DeserializeObject(jsonRsp);   //data should just be {"personId": "..."}
                foreach (JObject face in data)
                {
                    detectedIds.Add(face["faceId"].Value<string>());
                }

                return detectedIds;
            };
        }

        private async Task<FaceAPIResponse> MakeUnityRequestAsync(string purpose, string uri, byte[] reqBodyData, string bodyContentType, string method, Dictionary<string, string> requestParameters = null)
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
                throw new Exception("UnityWebRequest Error: " + client.error);
            }
            else
            {
                FaceAPIResponse rsp = new FaceAPIResponse
                {
                    response_type = (byte)client.responseCode,
                    response = client.downloadHandler.text
                };

                return rsp;
            }
            
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

    public class FaceAPICall<T>
    {
        public FaceAPIRequest request;
        public FaceAPIResponse response;

        public Func<Task<FaceAPIResponse>> apiCall;
        public Func<FaceAPIResponse, T> processResponse;
        public T defaultResult;
        private T result;
        private bool callSuccess;

        public async Task MakeCallAsync()
        {
            // theoretically, the http request should always get a response
            this.response = await apiCall.Invoke();

            try
            {
                this.result = processResponse.Invoke(this.response);
                callSuccess = true;
            }
            catch (Exception e)
            {
                this.result = defaultResult;
                Console.WriteLine(e.ToString());
                callSuccess = false;
            }
        }

        public T GetResult()
        {
            return result;
        }

        public bool SuccessfulCall()
        {
            return this.callSuccess;
        }

    }
}
