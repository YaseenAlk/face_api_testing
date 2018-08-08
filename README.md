
# PythonFaceIDHelper

This branch isolates the PythonFaceIDHelper project for use as a module in other projects! 

Here's instructions on using PythonFaceIDHelper in your own project as a git submodule:

## Adding this branch as a submodule
In your repository of interest, run the following commands:
```shell
git submodule add -b pythonfaceidhelper-submodule https://github.com/YaseenAlk/face_api_testing.git <path>
```
where `<path>` is the directory to put the submodule into.

This command clones the `pythonfaceidhelper-submodule` branch from the specified remote URL, `https://github.com/YaseenAlk/face_api_testing.git`.

Next, we need to initialize the submodule:
```shell
git submodule update --init
```

Finally, we can fetch and apply submodule updates using the following command:
```shell
git submodule update --remote
```

For more information on git submodules, check out this [third-party guide](https://www.activestate.com/blog/2014/05/getting-git-submodule-track-branch), this [guide](https://git-scm.com/book/en/v2/Git-Tools-Submodules) made by the documenters of git, and of course, the [official git documentation](https://git-scm.com/docs/git-submodule).

## Adding api_access_key.txt
Before PythonFaceIDHelper can be used, we need to add a file called `api_access_key.txt` somewhere in the repo folder. It should contain the following inside:
``` json
{
	"subscriptionKey":"<subkey>",
	"uriBase":"https://[location].api.cognitive.microsoft.com/face/v1.0/"
}
```
where `<subkey>` is your API subscription key and `[location]` is the region (e.g. westcentralus, eastus, ...).

## Using PythonFaceIDHelper
```python
# importing:
from <path>.faceapihelper.helper import FaceAPIHelper

# initializing helper
api_access_key = open(path_to_api_access_key.txt, "rb")                 # load api_access_key json
helper = FaceAPIHelper(api_access_key, "insert person group id here")   # create a FaceAPIHelper obj

# making API calls:
face_counter = helper.call_count_faces("/Desktop/image.png")    # create a FaceAPICall obj from helper
face_counter.make_call()                                        # make the API call here

num_faces_in_img = 0

if face_counter.was_call_successful():                          # check if API response was processed successfully
    num_faces_in_img = face_counter.result()                    # result() also has a default value for unsuccessful calls

# Access wrappers for FaceAPIRequest and FaceAPIResponse (part of face_msgs ROS package)
request_msg_wrapper = face_counter.request
response_msg_wrapper = face_counter.response
```
