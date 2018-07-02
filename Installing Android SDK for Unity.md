# Installing JDK and Android SDK for Unity App Deployment on Mac OS
## Yaseen Alkhafaji - July 2, 2018

## Step 1: Close any running instances of Unity and your script editor (probably MonoDevelop or Visual Studio)

## Step 2: Installing the JDK
### 1) Check which versions of JDK you might already have installed

This could also be done using the "ls" terminal command :P

#### 1a) Open a finder Window

#### 1b) Navigate to "Macintosh HD" (it's the directory that contains the folders "Applications", "Library", "Users", etc)

#### 1c) Navigate to the following folder: /Library/Java/JavaVirtualMachines
You may need to toggle hidden folders (keyboard shortcut is CMD+SHIFT+period (.)) if you don't see the "Library" folder.

#### 1d) Observe the folders in the directory.
Installed JDK directory names start with "jdk", followed by the version name, and end with ".jdk".
If there's no jdk folders in here, skip to 3).
If there's folders that start with "jdk1.9" or "jdk-10", continue to 2).
If there's at least one folder that starts with "jdk1.8" and neither of the above conditions apply, then skip to 4).

### 2) Delete any JDK versions that are above version 8, if they exist.
Unity currently only supports up to JDK 8, but if there are any installed JDK versions above 8, Java's settings will cause Unity to use an unsupported JDK version.

#### 2a) Note the versions that you want to delete

Then, using a terminal window:

#### 2b) For each version that you want to delete, run the following command:
``` shell
sudo rm -rf /Library/Java/JavaVirtualMachines/jdk<version>.jdk
```

where `<version>` is replaced with the jdk version that you want to delete

### 3) Download and install jdk1.8
[JDK 8 Download Link](http://www.oracle.com/technetwork/java/javase/downloads/jdk8-downloads-2133151.html). I'm using 8u171.

### 4) Set the java_home variable in the profile file
Not certain if this step is actually necessary, but better to be safe than sorry.

Using a terminal window:
#### 4a) Decide which profile file needs to be edited
Go to ~:
``` shell
cd ~
```
Then, list all of the files in the directory:

``` shell
ls -a
```

Scroll to the top of the list, where all of the files start with a period ("."):
If there's a file called .bash_profile, then you should edit this one.
If there's a file called .bash_login (and no file called .bash_profile), then you should edit this one.
If there's a file called .profile (and no file called .bash_profile or .bash_profile), then you should edit this one.

If none of them exist, then just use .bash_profile (not really sure if it makes a difference in this case).

#### 4b) Open the file selected in 4a) using a text editor.
I usually use nano, so I would enter the following into the terminal:
``` shell
nano <file_name>
```

#### 4c) Add the following lines to the file:

``` shell
JAVA_HOME=/Library/Java/JavaVirtualMachines/jdk<version>.jdk/Contents/Home
export JAVA_HOME;
```
Make sure to change <version> to one of the 1.8 versions that you installed in 3).

#### 4d) Save the file
In nano, it's Control+X to exit, then press 'y' to write changes, then press enter to save them.

## Step 3: Installing homebrew if not already installed

Using a terminal window:

### 1) Check if homebrew is already installed using the command `brew help`

### 1a) if it's not installed, install by going to brew.sh and following the instructions there (may require restarting the terminal after it's installed). [Link to homebrew download](https://brew.sh/)

## Step 4: Installing android-sdk using homebrew:
Run the following two commands:

``` shell
brew tap caskroom/cask
```

``` shell
brew cask install android-sdk
```

Make sure to restart any terminal windows afterward.

## Step 5: Downloading the actual sdk folder

### 1) Make a folder somewhere to store the SDK Root directory
It's not very important where, but it's important to remember its name and location.

### 2) Update sdkmanager just in case
Run the following command in terminal:

``` shell
sdkmanager --update
```

### 3) (only if necessary) Edit the sdkmanager script
I believe this issue happens if you install a new jdk version AFTER installing android-sdk using homebrew. Try going through the steps here only if 4) doesn't work. [source](https://stackoverflow.com/questions/47150410/failed-to-run-sdkmanager-list-android-sdk-with-java-9)


Using a terminal window:

#### 2a) Navigate to the installation location of sdkmanager
It should be /usr/local/bin, so run the following bash command:

``` shell
cd /usr/local/bin
```

#### 2b) Open sdkmanager with a text editor
It's just a script, so you can edit it with a text editor like nano:

``` shell
nano sdkmanager
```

#### 2c) Make the following addition
Find the line that sets the var DEFAULT_JVM_OPTS, and to the end of the string, add the following: `-XX:+IgnoreUnrecognizedVMOptions --add-modules java.se.ee`

Be careful with quotation marks. Here's what mine looks like:
``` shell
DEFAULT_JVM_OPTS='"-Dcom.android.sdklib.toolsdir=$APP_HOME" -XX:+IgnoreUnrecognizedVMOptions --add-modules java.se.ee'
```

#### 2d) Save and exit sdkmanager

In nano, it's Control+X to exit, then press 'y' to write changes, then press enter to save them.


### 4) Customize the following terminal command, and then run it:

``` shell
sdkmanager "tools" "build-tools;<build tools version>" "platforms;<platform version>" --sdk_root="<new sdk dir>"
```

`<build tools version>` is the version of build-tools to use. I'm using 28.0.1
`<platform version>` is the version of the sdk platform to use. I'm using android-28
`<new sdk dir>` is the folder you made to store the SDK root dir in 1)

To see the available versions, you can run `sdkmanager --list`

### 5) Follow the specified directions on the terminal
It's mostly just accepting licensing agreements, etc.

## Step 6: Replacing the tools folder

For some reason, Unity doesn't support the "tools" folder that comes with the latest version of android. If you are reading this in the future, maybe the issue is fixed, but if not, then follow these steps to replace it with a compatible version:
[source](http://answers.unity.com/answers/1326427/view.html)

### 1) Download the replacement folder
[Download link here](http://dl-ssl.google.com/android/repository/tools_r25.2.5-windows.zip)

### 2) Navigate to the sdk folder you made in the first part of Step 5

### 3) Move the existing "tools" folder somewhere else as a backup

### 4) Extract the replacement folder downloaded in 1), move it to the sdk folder, and ensure that its name is "tools"

## Step 7: Setting the build settings in Unity
### 1) Open Unity
### 2) In the toolbar above, navigate to Unity > Preferences > External Tools
There should be a section labelled Android with paths for "SDK", "JDK", and "NDK".

### 3) Set the SDK Path
Click Browse, and find the sdk folder you made in Step 5. The folder you select should contain folders such as "build-tools", "emulator", "platforms", ...
### 4) Set the JDK Path
Click Browse, and find the jdk folder that was observed in Step 2. The folder you select should be something along the lines of `/Library/Java/JavaVirtualMachines/jdk<version>.jdk/Contents/Home`
### 5) Set the NDK Path if necessary
I don't know much about this, but apparently it's needed for certain features.
---

And now hopefully you're all set! :D
If all has gone well, then you should be able to successfully build .apk files on Unity! (assuming that you have properly Switched Platforms in the Build Settings)