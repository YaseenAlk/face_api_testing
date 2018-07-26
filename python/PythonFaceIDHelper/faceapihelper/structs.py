from collections import namedtuple

# pythonic version of structs:
FaceAPIRequest = namedtuple('FaceAPIRequest',
                            ['request_method', 'request_type', 'content_type', 'request_parameters', 'request_body'])

FaceAPIResponse = namedtuple('FaceAPIResponse',
                             ['response_type', 'response'])


class FaceAPICall:
    def __init__(self, req, call, rsp_prcsr, default, argument_dict={}):
        self.request = req
        self.response = None

        self._api_call = call
        self._argument_dict = argument_dict
        self._response_processor = rsp_prcsr
        self._default_result = default
        self._result = None
        self._call_successful = None

    def make_call(self):
        self.response = self._api_call(**self._argument_dict)
        try:
            self._result = self._response_processor(self.response)
            self._call_successful = True
        except Exception as e:
            self._result = self._default_result
            self._call_successful = False
            print("Error when invoking FaceAPICall.make_call(): ", e)

    def result(self):
        return self._result

    def was_call_successful(self):
        return self._call_successful
