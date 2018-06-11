# face_matching
Testing Facial Recognition with Microsoft's Face API

To run on your computer, add a file called `api_access_key.txt` to the repo folder, and paste the following inside:
``` json
{
	"subscriptionKey":"<subkey>",
	"uriBase":"https://[location].api.cognitive.microsoft.com/face/v1.0/"
}
```
where `<subkey>` is your API subscription key and `[location]` is the region (e.g. westcentralus, eastus, ...).

## Useful ffmpeg functions (for future reference)
### To extract frames at `n` fps from a source video, starting `x` minutes into the video and lasting `y` minutes

``` shell
ffmpeg -ss 00:0x:00 -i "<src>" -t 00:0y:00 -vf fps=3 "<path>/frame%05d.bmp"
```

where `-ss 00:0x:00` indicates the starting timestamp,

`-i "<src>"` is the file path to the source video, 

`-t 00:0y:00` indicates the duration of the excerpt (so for a starting time of `x` minutes and a duration of `y` minutes, the timestamp range is from `x` to `(x + y)`), 

`-vf fps=3` is the list of filters (in this case, the only filter is 3 frames-per-second), 

and `"<path>/frame%05d.bmp"` is the output file path (the `%05d` is an int that increments by 1 for each frame, and has up to 4 leading 0s in front of the digit; so the folder will be full of files named `frame00001.bmp, frame00002.bmp, frame00003.bmp, ...`). Note that ffmpeg supports other image files, such as .jpg, .png, etc, and will format the image accordingly.

### To halve the width and height of a video
```shell
ffmpeg -i "<src>" -vf scale=iw/2:ih/2 "<output path>"
```
where `iw` is a variable for the input width and `ih` is a variable for the input height

[Here](https://trac.ffmpeg.org/wiki/Scaling) is more information on scaling

And [here](https://ffmpeg.org/ffmpeg.html) is the full site for ffmpeg documentation
