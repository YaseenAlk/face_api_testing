# face_matching
Testing Facial Recognition with Microsoft's Face API

To run on your computer, add a file called `api_access_key.txt` to the repo folder, and paste the following inside:
``` json
{
	"subscriptionKey":"<subkey>",
	"uriBase":"https://[location].api.cognitive.microsoft.com/face/v1.0/"
}
```
where `subkey` is your API subscription key and `[location]` is the region (e.g. westcentralus, eastus, ...).