﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CategorizeIdentifiedVids
{
    class Program
    {

        const int NUM_BANNEKER = 58;    //number of unique participants from banneker
        const int NUM_JFK = 39;         //number of unique participants from jfk

        const string INT_FORMAT = "D3"; //formats "3" into "003", "21" into "021", etc 
        const string FILE_EXT = ".MP4";
        const string GUESS_FILE = "guesses.json";

        static string vidPath, guessPath;
        static bool moveFiles = false;


        // Usage: dotnet CategorizeIdentifiedVids.dll <path to videos> <path to guesses> [-move_files (optional)]
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dotnet CategorizeIdentifiedVids.dll <path to videos> <path to guesses> [-move_files (optional)]");
                return;
            }
            if (Directory.Exists(args[0]))
            {
                vidPath = args[0];
            }
            else
            {
                Console.WriteLine("Directory '" + args[0] + "' does not exist.");
                return;
            }

            if (Directory.Exists(args[1]))
            {
                guessPath = args[1];
            }
            else
            {
                Console.WriteLine("Directory '" + args[1] + "' does not exist.");
                return;
            }

            if (Array.IndexOf(args, "-move_files") > -1)
            {
                moveFiles = true;
            }

            CategorizeSchool("banneker", NUM_BANNEKER);
            CategorizeSchool("jfk", NUM_JFK);

        }

        static void CategorizeSchool(string schoolName, int numParticipants)
        {
            string schoolGuessPath = Path.Join(guessPath, schoolName);
            if (!Directory.Exists(schoolGuessPath))
            {
                Console.WriteLine("Path '" + schoolGuessPath + "' does not exist.");
                return;
            }

            string schoolVidPath = Path.Join(vidPath, "unlabelled_" + schoolName);
            if (!Directory.Exists(schoolVidPath))
            {
                Console.WriteLine("Path '" + schoolVidPath + "' does not exist.");
                return;
            }

            Console.WriteLine("Processing '" + schoolName + "'...");

            Dictionary<string, string> matches = new Dictionary<string, string>();

            string[] unlabelledVidPaths = Directory.GetFiles(schoolVidPath, "*" + FILE_EXT);
            foreach (string vid in unlabelledVidPaths)
            {
                string fileName = Path.GetFileName(vid);
                
                string guessFolderPath = Path.Join(schoolGuessPath, fileName);
                if (!Directory.Exists(guessFolderPath))
                {
                    Console.WriteLine("Weird! Looks like file '" + fileName + "' from school '" + schoolName + "' was never even processed! (no guess folder found) Skipping...");
                    continue;
                }

                string jsonFilePath = Path.Join(guessFolderPath, GUESS_FILE);
                if (!File.Exists(jsonFilePath))
                {
                    Console.WriteLine("Weird! Looks like file '" + fileName + "' from school '" + schoolName + "' was never identified! (no " + GUESS_FILE + " found) Skipping...");
                    continue;
                }

                string final_rec = GetFinalRecFromJSONPath(jsonFilePath);
                matches.Add(fileName, final_rec); 

                if (moveFiles)
                {
                    string subjectFolder = Path.Join(schoolVidPath, final_rec.ToLower());
                    if (!Directory.Exists(subjectFolder))
                    {
                        Directory.CreateDirectory(subjectFolder);
                        string mislabelled = Path.Join(subjectFolder, "mislabelled");
                        Directory.CreateDirectory(mislabelled);
                    }

                    string oldPath = vid;
                    string newPath = Path.Join(subjectFolder, fileName);

                    File.Move(oldPath, newPath);
                }
            }

            GenerateCSVFromMatches(matches, "guesses_" + schoolName + ".csv");

            Console.WriteLine("Done processing '" + schoolName + "'!");
        }

        static string GetFinalRecFromJSONPath(string path)
        {
            string json = System.IO.File.ReadAllText(path);
            JObject data = (JObject) JsonConvert.DeserializeObject(json);
            return data["result"]["final_recommendation"].Value<string>();
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

        static void GenerateCSVFromMatches(Dictionary<string, string> matches, string fileName)
        {
            string savePath = Path.Join(vidPath, fileName);

            List<string> dataToSave = new List<string>();
            foreach (KeyValuePair<string, string> entry in matches)
            {
                dataToSave.Add(entry.Key + "," + entry.Value);
            }
            System.IO.File.WriteAllLines(savePath, dataToSave);
        }

    }
}
