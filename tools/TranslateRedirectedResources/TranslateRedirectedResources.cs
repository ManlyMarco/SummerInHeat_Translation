using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace TranslateRedirectedResources
{
    class TranslateRedirectedResources
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                var toDecompile = args.Where(File.Exists).Where(x => string.Equals(Path.GetExtension(x), ".txt", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath).ToArray();
                if (toDecompile.Length > 0)
                {
                    using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(toDecompile[0]) ?? throw new Exception("How? " + toDecompile[0]), "_LineDump-orig"), false, Encoding.UTF8))
                    {
                        foreach (var path in toDecompile)
                            OriginalToCsv(path, writer);
                    }
                    return;
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Crashed! The extracted files are likely to be bad!\n" + e);
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            string rootFolder;
            try
            {
                rootFolder = args.FirstOrDefault(x => string.Equals(Path.GetFileName(x), "Translation", StringComparison.OrdinalIgnoreCase) && Directory.Exists(x));
                if (rootFolder == null)
                {
                    rootFolder = Path.Combine(Environment.CurrentDirectory, "GameData/BepInEx/Translation");
                    if (!Directory.Exists(rootFolder))
                    {
                        // Allow running directly on the IO_Translation repo
                        rootFolder = Path.Combine(Environment.CurrentDirectory, "Translation");
                        if (!Directory.Exists(rootFolder))
                            throw new Exception("Cold not find the Translation folder! Place this next to the GameData or the Translation folder!");
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to get Translation folder! " + e.Message);
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            try
            {
                ApplyTranslationsToDump(rootFolder);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Crashed! The dump may be partially translated in the output directory!\n" + e);
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
        }

        private static void ApplyTranslationsToDump(string rootFolder)
        {
            var sw = Stopwatch.StartNew();
            int hitsTl = 0, hitsDump = 0, missDump = 0;

            string inputFolder = Path.Combine(rootFolder, "en/Text");
            string translatedFileName;
            string outputFolder = Path.Combine(rootFolder, "en/RedirectedResources/assets/summerinheat_data/data/masterscenario/");
            string dumpFolder = Path.Combine(rootFolder, "en/RedirectedResources/assets/summerinheat_data/data/OriginalDump");

            Dictionary<string, string> TranslationsDictionary = new Dictionary<string, string>();
            string value;

            string[] fileNames = Directory.GetFiles(dumpFolder);

            string[] inputFileNames = Directory.GetFiles(inputFolder);

            //=========================== Populating Translation Dictionary ==========================================
            for (int i = 0; i < inputFileNames.Length; i++)
            {
                translatedFileName = inputFileNames[i];
                if (!translatedFileName.StartsWith("_"))
                {
                    string[] translatedFile = File.ReadAllLines(translatedFileName);
                    foreach (string line in translatedFile)
                    {
                        if (line.StartsWith("//")) continue;

                        string[] parts = line.Split(new char[] { '=' }, 2, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            hitsTl++;

                            var key = parts[0].Replace("\\n　", "");
                            key = key.Replace("\\n ", "");
                            key = key.Replace("\\n", "");
                            key = key.Replace("\"", "");
                            key = key.Replace("「", "『");
                            key = key.Replace("」", "』");
                            value = parts[1];
                            if (!TranslationsDictionary.ContainsKey(key))
                                TranslationsDictionary.Add(key, value);
                        }
                    }
                }
            }


            //======================================== Translating Dumped Files ============================================
            for (int fileIndex = 0; fileIndex < fileNames.Length; fileIndex++)
            {

                string thisFile = fileNames[fileIndex];

                if (!string.Equals(Path.GetExtension(thisFile), ".txt", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] dumpedFile = File.ReadAllLines(thisFile);
                string[] outputFile = new string[dumpedFile.Length];

                //If is inside a group of lines, this number is positive
                int groupLenght = 0;

                int currentIndex = 0;
                int nextIndex;

                string thisLine;
                string nextLine;
                string translatedLine;
                string key = "";

                for (int dumpPos = 0; dumpPos < dumpedFile.Length - 1; dumpPos++)
                {
                    thisLine = dumpedFile[dumpPos];
                    nextLine = dumpedFile[dumpPos + 1];

                    //As "fileIndex" advances, the number of remaining lines in a group of lines decreases
                    groupLenght--;

                    //Check for useless lines
                    var thisLineTrimmed = thisLine.TrimStart();
                    if (thisLineTrimmed != "" &&
                        !thisLineTrimmed.StartsWith("***") &&
                        !_CharacterNames.ContainsKey(thisLineTrimmed) &&
                        !thisLineTrimmed.Contains("CH") &&
                        !thisLineTrimmed.Contains("//") &&
                        groupLenght <= 0)
                    {
                        //================= Making the key =================
                        key = thisLine;

                        // Treating Group of Lines
                        // Making the key the same as translated file, and also defining the lenght of the group of lines
                        if (thisLine != "" && nextLine != "" && !nextLine.Contains("//") && !nextLine.StartsWith("***"))
                        {
                            groupLenght = 1;
                            bool groupEnd = false;
                            while (!groupEnd)
                            {
                                nextIndex = dumpPos + groupLenght;
                                //key = key + "\\n" + dumpedFile[nextIndex];
                                key = key + (dumpedFile[nextIndex].TrimStart('　')).TrimStart(' ');

                                //groupLenght increases until the end of consecutive valid lines
                                groupLenght++;

                                nextIndex = dumpPos + groupLenght;
                                if (dumpedFile[nextIndex] == "" || dumpedFile[nextIndex].StartsWith("//") || dumpedFile[nextIndex].StartsWith("***"))
                                    groupEnd = true;
                            }
                        }

                        key = key.Replace("「", "『");
                        key = key.Replace("」", "』");
                        key = key.Replace("\"", "");

                        //Translating lines found with the key
                        if (TranslationsDictionary.ContainsKey(key))
                        {
                            //Splitting lines in separated words
                            string originalTranslation = TranslationsDictionary[key];
                            string[] words = originalTranslation.Split(' ');
                            int numWords = words.Length;

                            // making a new line if lenght exceeds is maximum
                            translatedLine = "";
                            int maxLenght = 50;
                            int currentLenght = 0;
                            int currentLine = 1;

                            //Miconisomi only allows up to 3 lines per screen
                            float lineNumber = (float)originalTranslation.Length / (float)maxLenght;
                            //if there's more than 3 lines, split the chars in these 3 lines
                            if (lineNumber > 3f)
                                maxLenght = originalTranslation.Length / 3;

                            for (int wordIndex = 0; wordIndex < numWords; wordIndex++)
                            {
                                if ((currentLenght + words[wordIndex].Length) > maxLenght && currentLine < 3)
                                {
                                    translatedLine += "\n";
                                    // Original scripts have IDSP(wide space) at start of new line if it's a part of quoted text
                                    if (key.Contains("『")) 
                                        translatedLine += "\u3000";
                                    currentLenght = 0;
                                    currentLine++;
                                }

                                translatedLine = translatedLine + words[wordIndex] + " ";
                                currentLenght += words[wordIndex].Length + 1;
                            }

                            translatedLine = PostTranslation(dumpedFile[dumpPos], translatedLine);
                            outputFile[currentIndex] = translatedLine;
                            currentIndex++;

                            hitsDump++;
                        }
                        //if current line was not found in the dictionary, just copy
                        else
                        {
                            outputFile[currentIndex] = dumpedFile[dumpPos];
                            currentIndex++;

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Translation miss: " + key);
                            Console.ForegroundColor = ConsoleColor.White;
                            missDump++;
                        }
                    }
                    //if current line is not a useful line, just copy
                    else
                    {
                        if (groupLenght <= 0 || !TranslationsDictionary.ContainsKey(key))
                        {
                            var line = dumpedFile[dumpPos];

                            // Replace character names - disabled because it breaks Cocoa If route, possibly others
                            //if (!thisLineTrimmed.StartsWith("***") &&
                            //    !thisLineTrimmed.Contains("//"))
                            //{
                            //    foreach (var characterName in _CharacterNames.OrderByDescending(x => x.Key.Length))
                            //    {
                            //        line = line.Replace(characterName.Key, characterName.Value);
                            //    }
                            //}

                            outputFile[currentIndex] = line;
                            currentIndex++;
                        }
                    }
                }

                //Copying the last line
                outputFile[currentIndex] = dumpedFile[dumpedFile.Length - 1];



                //Finally, writing the file!
                string filePath = outputFolder + Path.GetFileName(thisFile);
                var contents = string.Join("\n", outputFile);
                // Remove excessive newlines, keep at most 3 in a row
                contents = Regex.Replace(contents, "\n\n\n\n+", "\n\n\n");
                // Ensure all newlines are \r\n
                contents = contents.Replace("\r\n", "\n").Replace("\n", "\r\n");
                File.WriteAllText(filePath, contents);
            }

            //Exit dialogue
            //Console.WriteLine("Done, press enter to exit");
            //Console.ReadLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Finished in {sw.ElapsedMilliseconds}ms! Got {hitsTl} translated lines from {inputFileNames.Length} translation files. Replaced {hitsDump} dialog lines in dump ({missDump} failed).");
            Console.ForegroundColor = ConsoleColor.White;
        }

        static string PostTranslation(string dump, string translation)
        {
            dump = dump.Replace("「", "『");
            dump = dump.Replace("」", "』");
            translation = translation.TrimEnd(' ');

            if (dump.Contains("『"))
            {
                translation = translation.TrimStart('"', '“', '”');
                translation = translation.TrimEnd('"', '“', '”');
                translation = "『" + translation + "』";
            }

            return translation;
        }

        private static void OriginalToCsv(string path, StreamWriter globalWriter)
        {
            var lines = File.ReadAllLines(path);

            using (var writer = new StreamWriter(Path.ChangeExtension(path, ".csv"), false, Encoding.UTF8))
            {
                writer.WriteLine("Character,Text");

                // Turn the original script into a CSV file for translation
                // The CSV will have the character name in first column (if available), and the combined text in the second column
                // Non-dialogue lines will be ignored
                var currentCharacter = "";
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (IsDialogBreak(line))
                    {
                        currentCharacter = "";
                        continue;
                    }

                    var isCharacterLine = _CharacterNames.ContainsKey(line) || line.Contains("CH");

                    if (isCharacterLine)
                    {
                        currentCharacter = line;
                        continue;
                    }

                    // Combine dialog lines
                    var combined = lines[i];
                    i++;
                    while (i < lines.Length)
                    {
                        line = lines[i].Trim();
                        if (IsDialogBreak(line))
                        {
                            --i;
                            break;
                        }

                        combined += line;
                        i++;
                    }

                    var csvLine = $"\"{currentCharacter}\",\"{combined}\"";

                    writer.WriteLine(csvLine);
                    globalWriter.WriteLine(combined);
                }
            }
        }

        private static bool IsDialogBreak(string line)
        {
            return line.StartsWith("***") || line.Contains("//") || string.IsNullOrEmpty(line);
        }

        private static readonly Dictionary<string, string> _CharacterNames = new Dictionary<string, string>
        {
            { "小鳥遊",        "Takanashi" }   ,
            { "乙羽",             "Otoha" }   ,
            { "小鳥遊 乙羽",       "Takanashi Otoha"}    ,
            { "小鳥遊 乙羽（回想）","Takanashi Otoha (Recollection)" }   ,
            { "小鳥遊 乙羽（独白）","Takanashi Otoha (Monologue)" }   ,
            { "暁",              "Akatsuki" }   ,
            { "莉乃",             "Rino" }   ,
            { "暁 莉乃",          "Akatsuki Rino" }   ,
            { "暁 莉乃（独白）",   "Akatsuki Rino (Monologue)" }   ,
            { "麗子",             "Reiko" }   ,
            { "小鳥遊 麗子",       "Takanashi Reiko"}    ,
            { "小鳥遊 麗子（独白）","Takanashi Reiko (Monologue)" }   ,
            { "田中",             "Tanaka" }   ,
            { "心々愛",           "Cocoa" }   ,
            { "田中 心々愛",       "Tanaka Cocoa"}    ,
            { "田中 心々愛（独白）","Tanaka Cocoa (Monologue)" }   ,
            { "回想",             "Recollection" }   ,
            { "独白",             "Monologue" }   ,
            { "主人公",           "Hero" }   ,
            { "茂部",             "Mobu" }   ,
            { "たかし",           "Takashi" }   ,
            { "？？？",           "???" }   ,
            { "教師",             "Teacher" }   ,
            { "校長",             "Principal" }   ,
            { "隣の男子生徒",      "Male student from next door" }   ,
            { "女子部員Ａ",        "Female member A"}    ,
            { "女子部員Ｂ",        "Female member B"}    ,
            { "女子部員Ｃ",        "Female member C"}    ,
            { "女子部員Ｄ",        "Female member D"}    ,
            { "女子部員Ｅ",        "Female member E"}    ,
            { "友人Ａ",           "Friend A" }   ,
            { "友人Ｂ",           "Friend B" }   ,
        };
    }
}
