""""
python port of the Unity/C# project 'UnityFaceIDHelper'
Unity version can be found at https://github.com/mitmedialab/face_matching/tree/unity-helper-face-msgs
in the folder "unity"

---
usage:
---

# importing:
from faceapihelper.helper import FaceAPIHelper as helper

# making API calls:
face_counter = helper.call_count_faces("/Desktop/image.png")    # create a FaceAPICall obj
face_counter.make_call()                                        # make the API call here

num_faces_in_img = 0

if face_counter.was_call_successful():                          # check if API response was processed successfully
    num_faces_in_img = face_counter.result()                    # result() also has a default value for unsuccessful calls

# Access wrappers for FaceAPIRequest and FaceAPIResponse (part of face_msgs ROS package)
request_msg_wrapper = face_counter.request
response_msg_wrapper = face_counter.response
"""
from faceapihelper.structs import FaceAPICall as call_struct
from faceapihelper.structs import FaceAPIRequest as req_struct
from faceapihelper.structs import FaceAPIResponse as rsp_struct
from faceapihelper.facemsgenums import RequestMethod as r_method_enum
from faceapihelper.facemsgenums import RequestType as r_type_enum
from faceapihelper.facemsgenums import ContentType as c_type_enum

from pathlib import Path
from enum import Enum
import requests
import json


class FaceAPIHelper:

    def __init__(self, api_access_key, p_grp_id):
        self.sub_key = self.read_json_param_from_str(api_access_key, "subscriptionKey")
        self.uri_base = self.read_json_param_from_str(api_access_key, "uriBase")
        self.person_group_id = p_grp_id

    # All public methods start with "call" (e.g. call_count_faces)
    # 'private' methods are surrounded by underscores (e.g. _enforce_byte_array_)

    def call_count_faces(self, img_data):
        img_data = self._enforce_byte_array_(img_data)

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.FACE_DETECT.value,
                         c_type_enum.CONTENT_STREAM.value, "", img_data)
        api_call = self._rsp_detect_for_identifying_
        rsp_prcsr = self._process_rsp_count_faces_
        default_val = -1 # normally returns the number of faces detected in the img
        arg_dict = {'img_data': img_data}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_get_large_person_group_training_status(self):
        empty = "{}".encode('utf-8')

        req = req_struct(r_method_enum.HTTP_GET.value, r_type_enum.LARGEPERSONGROUP_GETTRAININGSTATUS.value,
                         c_type_enum.CONTENT_JSON.value, "", empty)
        api_call = self._rsp_get_large_person_group_training_status_
        rsp_prcsr = self._process_rsp_get_large_person_group_training_status_
        default_val = TrainingStatus.TRAINING_API_ERROR # normally returns a string (TrainingStatus Enum values)

        call = call_struct(req, api_call, rsp_prcsr, default_val)
        return call

    def call_start_training_large_person_group(self):
        empty = "{}".encode('utf-8')

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.LARGEPERSONGROUP_TRAIN.value,
                         c_type_enum.CONTENT_JSON.value, "", empty)
        api_call = self._rsp_start_training_large_person_group_
        rsp_prcsr = self._process_rsp_start_training_large_person_group_
        default_val = False # normally returns True if the "Start Training" call is successful

        call = call_struct(req, api_call, rsp_prcsr, default_val)
        return call

    def call_get_name_from_large_person_group_person_person_id(self, person_id):
        empty = "{}".encode('utf-8')

        req = req_struct(r_method_enum.HTTP_GET.value, r_type_enum.LARGEPERSONGROUPPERSON_GET.value,
                         c_type_enum.CONTENT_JSON.value, "", empty)
        api_call = self._rsp_get_name_from_large_person_group_person_person_id_
        rsp_prcsr = self._process_rsp_get_name_from_large_person_group_person_person_id_
        default_val = "" # normally returns a name
        arg_dict = {'person_id': person_id}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_delete_face_from_large_person_group_person(self, person_id, persisted_face_id):
        empty = "{}".encode('utf-8')

        req = req_struct(r_method_enum.HTTP_DELETE.value, r_type_enum.LARGEPERSONGROUPPERSON_DELETEFACE.value,
                         c_type_enum.CONTENT_JSON.value, "", empty)
        api_call = self._rsp_delete_face_from_large_person_group_person_
        rsp_prcsr = self._process_rsp_delete_face_from_large_person_group_person_
        default_val = False # normally returns True if deletion is successful
        arg_dict = {'person_id': person_id, 'persisted_face_id': persisted_face_id}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_add_face_to_large_person_group_person(self, person_id, img_data):
        img_data = self._enforce_byte_array_(img_data)

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.LARGEPERSONGROUPPERSON_ADDFACE.value,
                         c_type_enum.CONTENT_STREAM.value, "", img_data)
        api_call = self._rsp_add_face_to_large_person_group_person_
        rsp_prcsr = self._process_rsp_add_face_to_large_person_group_person_
        default_val = "" # normally returns a persistedFaceId
        arg_dict = {'person_id': person_id, 'img_data': img_data}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_create_large_person_group_person(self, name, data=""):
        req_body = ('{"name": "' + name + '", "userData": "' + data + '"}').encode('utf-8')

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.LARGEPERSONGROUPPERSON_CREATE.value,
                         c_type_enum.CONTENT_JSON.value, "", req_body)
        api_call = self._rsp_create_large_person_group_person_
        rsp_prcsr = self._process_rsp_create_large_person_group_person_
        default_val = "" # normally returns a personId
        arg_dict = {'name': name, 'data': data}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_identify_from_face_id(self, face_id):
        req_body = ('{"largePersonGroupId": "' + self.person_group_id + '", "faceIds": ["' + face_id + '"]}').encode('utf-8')

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.FACE_IDENTIFY.value,
                         c_type_enum.CONTENT_JSON.value, "", req_body)
        api_call = self._rsp_identify_from_face_id_
        rsp_prcsr = self._process_rsp_identify_from_face_id_
        default_val = None # normally returns a dictionary: key=personId (guess), value=confidence of guess
        arg_dict = {'face_id': face_id}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def call_detect_for_identifying(self, img_data):
        img_data = self._enforce_byte_array_(img_data)

        req = req_struct(r_method_enum.HTTP_POST.value, r_type_enum.FACE_DETECT.value,
                         c_type_enum.CONTENT_STREAM.value, "", img_data)
        api_call = self._rsp_detect_for_identifying_
        rsp_prcsr = self._process_rsp_detect_for_identifying_
        default_val = None # normally returns a list of faceIds detected in the image
        arg_dict = {'img_data': img_data}

        call = call_struct(req, api_call, rsp_prcsr, default_val, argument_dict=arg_dict)
        return call

    def _rsp_identify_from_face_id_(self, face_id):
        uri = self.uri_base + "identify"
        encoded = ('{"largePersonGroupId": "' + self.person_group_id + '", "faceIds": ["' + face_id + '"]}').encode('utf-8')
        rsp = self.make_request("Identify person using faceId", uri, encoded, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_POST)
        return rsp

    def _process_rsp_identify_from_face_id_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        faceAndCandidates = json.loads(json_rsp[0])

        idsAndConfidences = {}

        candidateList = json.loads(faceAndCandidates["candidates"])
        for cand in candidateList:
            idsAndConfidences[cand["personId"]] = cand["confidence"]

        return idsAndConfidences


    def _rsp_create_large_person_group_person_(self, name, data):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/persons"
        encoded = ('{"name": "' + name + '", "userData": "' + data + '"}').encode('utf-8')
        rsp = self.make_request("Adding Person to Person Group", uri, encoded, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_POST)
        return rsp

    def _process_rsp_create_large_person_group_person_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp["personId"]

    def _rsp_add_face_to_large_person_group_person_(self, person_id, img_data):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/persons/" + person_id + "/persistedFaces?"
        img = img_data
        rsp = self.make_request("Adding Image to " + person_id, uri, img, c_type_enum.CONTENT_STREAM, r_method_enum.HTTP_POST)

    def _process_rsp_add_face_to_large_person_group_person_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp["persistedFaceId"]

    def _rsp_delete_face_from_large_person_group_person_(self, person_id, persisted_face_id):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/persons/" + person_id + "/persistedFaces/" + persisted_face_id
        empty = "{}".encode('utf-8')
        rsp = self.make_request("Removing Image from " + person_id, uri, empty, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_DELETE)
        return rsp

    def _process_rsp_delete_face_from_large_person_group_person_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp == ""

    def _rsp_get_name_from_large_person_group_person_person_id_(self, person_id):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/persons/" + person_id
        empty = "{}".encode('utf-8')
        rsp = self.make_request("Retrieve Person from ID", uri, empty, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_GET)
        return rsp

    def _process_rsp_get_name_from_large_person_group_person_person_id_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp["name"]

    def _rsp_start_training_large_person_group_(self):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/train"
        empty = "{}".encode('utf-8')
        rsp = self.make_request("Training the " + self.person_group_id + " LargePersonGroup using the added images",
                                uri, empty, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_POST)
        return rsp

    def _process_rsp_start_training_large_person_group_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp == ""

    def _rsp_get_large_person_group_training_status_(self):
        uri = self.uri_base + "largepersongroups/" + self.person_group_id + "/training"
        empty = "{}".encode('utf-8')
        rsp = self.make_request("Check training status", uri, empty, c_type_enum.CONTENT_JSON, r_method_enum.HTTP_GET)
        return rsp

    def _process_rsp_get_large_person_group_training_status_(self, api_rsp):
        json_rsp = json.loads(api_rsp.response)
        return json_rsp["status"]

    def _rsp_detect_for_identifying_(self, img_data):
        uri = self.uri_base + "detect"
        img = img_data
        rsp = self.make_request("Detect faces in an img", uri, img, c_type_enum.CONTENT_STREAM, r_method_enum.HTTP_POST)
        return rsp

    def _process_rsp_detect_for_identifying_(self, api_rsp):
        detected_ids = []
        json_rsp = json.loads(api_rsp.response)
        for face in json_rsp:
            detected_ids.append(face['faceId'])
        return detected_ids

    def _process_rsp_count_faces_(self, api_rsp):
        detected_ids = self._process_rsp_detect_for_identifying_(api_rsp)
        return len(detected_ids)

    def make_request(self, purpose, uri, req_body_data, body_content_type, method, request_params={}):
        if self.sub_key == "" or self.uri_base == "":
            raise FileNotFoundError("Please make sure that api_access_key.txt is in the correct location.")

        req_headers = {"Ocp-Apim-Subscription-Key": self.sub_key, "Content-Type": body_content_type.value}

        if method == r_method_enum.HTTP_POST:
            response = requests.post(uri, data=req_body_data, headers=req_headers, params=request_params)
        elif method == r_method_enum.HTTP_PUT:
            response = requests.put(uri, data=req_body_data, headers=req_headers, params=request_params)
        elif method == r_method_enum.HTTP_GET:
            response = requests.get(uri, data=req_body_data, headers=req_headers, params=request_params)
        elif method == r_method_enum.HTTP_DELETE:
            response = requests.delete(uri, data=req_body_data, headers=req_headers, params=request_params)
        elif method == r_method_enum.HTTP_PATCH:
            response = requests.patch(uri, data=req_body_data, headers=req_headers, params=request_params)
        else:
            raise ValueError("Unknown RequestMethod specified!")

        request_struct = rsp_struct(response.status_code, response.content)
        return request_struct

    def get_image_as_byte_array(self, img_file_path):
        with open(img_file_path, "rb") as imageFile:
            f = imageFile.read()
            b = bytearray(f)

        return b

    def read_json_param_from_str(self, string, param):
        dictionary = json.load(str(string))
        return dictionary[param]

    # takes in an image file path or an image byte array, and always returns an image byte array! :)
    def _enforce_byte_array_(self, img_data):
        if isinstance(img_data, str):   # gotta love duck typing
            path_check = Path(img_data)
            if path_check.is_file():
                return self.get_image_as_byte_array(img_data)
            else:
                raise ValueError("Argument is not a valid file path!")
        else:
            try:
                img_data.decode()
                return img_data
            except AttributeError:
                raise ValueError("Argument is not a valid image byte array!")


class TrainingStatus(Enum):
    TRAINING_SUCCEEDED = "succeeded"
    TRAINING_FAILED = "failed"
    TRAINING_RUNNING = "running"
    TRAINING_API_ERROR = ""
