using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TfsMethodChanges
{
    /// <summary>
    /// reads the contents of an iniFile and returns all sections of it
    /// </summary>
    class IniFileParser
    {
        private const string s_IniFileComment = ";";
        private const string s_KeyValuePairAssignmentChar = "=";

        private const string s_IniFileStreamError = "IniFile Error: the IniFile could no be read.\r\n";

        private const string s_IniFileStartingError =
            "IniFile Error: the IniFile has to start with a Section\r\n";

        /// <summary>
        /// Regex used to check if a line is a sectionHeader,
        /// a sectionHeader has to be between two brackets
        /// further it may consist of up to two combined strings 
        /// (with a whitespace between them) with any
        /// leading or trailing whitespaces which will be filtered
        /// \S implies all characters except the whitespace characters
        /// </summary>
        private static readonly Regex s_SectionHeaderRegex = new Regex(@"\[\s*(\S+\s*?\S*)\s*\]");

        /// <summary>
        /// Regex used to filter out the comments in a line,
        /// before a command there always has to be a whitespace character
        /// otherwise it would be treated as a part of the value
        /// </summary>
        private static readonly Regex s_CommentsFilterRegex = new Regex
            (@"(\s+" + s_IniFileComment + "(.*))");

        /// <summary>
        /// Regex used to get the key and the value of a KeyValuePair from a line,
        /// any leading or trailing whitespaces of the s_KeyValuePairAssignmentChar 
        /// are ignored
        /// </summary>
        private static readonly Regex s_KeyValuePairRegex = new Regex
            (@"(.*?)\s*" + s_KeyValuePairAssignmentChar + @"\s*(.*)\s*");

        private const int s_KeyGroupIndex = 1;
        private const int s_ValueGroupIndex = 2;
        private const int s_SectionHeaderGroupIndex = 1;

        /// <summary>
        /// reads the iniFile stream and creates an enumerable of all sections with
        /// their corresponding KeyValuePairs
        /// </summary>
        /// <param name="iniStream">contains the Stream of the IniFile</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: an enumerable of all sections, null otherwise</returns>
        public static IEnumerable<Section> ReadIniFile(
            Stream iniStream,
            StreamWriter errorLogWriter)
        {
            if (iniStream == null)
            {
                errorLogWriter.Write(s_IniFileStreamError);
                return null;
            }

            IEnumerable<Section> sections = new List<Section>();

            IEnumerable<string> iniFileContent = ReadAllLines(iniStream);

            //get all lines, which are not commentLines or empty lines
            //and their corresponding line numbers
            IEnumerable<Line> lines = iniFileContent.Select
                ((line,
                    lineNumber) => new Line {LineContent = line.Trim(), LineNumber = lineNumber})
                .Where
                (line =>
                    !(line.LineContent.StartsWith(s_IniFileComment) ||
                      line.LineContent.Equals(String.Empty)));

            //an empty list of lines indicates that the iniFile contains
            //only comments or is empty
            if (lines.Any())
            {
                string firstLine = lines.First().LineContent;

                //check if the iniFile starts with a Section
                if (IsSectionHeader(firstLine))
                {
                    //filter out all partial comments
                    IEnumerable<Line> linesWithoutComments = lines.Select
                        (line =>
                            new Line
                            {
                                LineContent = FilterComments(line.LineContent),
                                LineNumber = line.LineNumber
                            });

                    //group lines into sections
                    IEnumerable<IEnumerable<Line>> sectionsGrouping = SplitLinesIntoSections
                        (linesWithoutComments);

                    //create the sections
                    sections = CreateSections(sectionsGrouping, errorLogWriter);
                }
                else
                {
                    errorLogWriter.Write(s_IniFileStartingError);
                    return null;
                }
            }

            return sections;
        }

        /// <summary>
        /// reads all lines of the iniStream
        /// </summary>
        /// <param name="iniStream">contains the Stream of the IniFile</param>
        /// <returns>a list of all lines in the iniFile</returns>
        private static IEnumerable<string> ReadAllLines(Stream iniStream)
        {
            List<String> lines = new List<String>();
            iniStream.Position = 0;

            using (StreamReader sr = new StreamReader(iniStream))
            {
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// .) removes the comments in a line. 
        /// .) returns the lineContent unchanged if it does not contain any comments. 
        /// </summary>
        /// <param name="lineContent">the content of a line</param>
        /// <returns>the lineContent without comments</returns>
        private static string FilterComments(string lineContent)
        {
            Match match = s_CommentsFilterRegex.Match(lineContent);
            string result = lineContent.Remove(match.Index, match.Length);

            return result;
        }

        /// <summary>
        /// splits the lines into sections, each section starts with 
        /// a sectionHeader and contains exactly one sectionHeader
        /// </summary>
        /// <param name="lines">contains a list of lines with their 
        /// lineContents and lineNumbers</param>
        /// <returns>on success: a list of all lines grouped by sections, 
        /// an empty list otherwise</returns>
        private static IEnumerable<IEnumerable<Line>> SplitLinesIntoSections(
            IEnumerable<Line> lines)
        {
            var items = new List<Line>();

            foreach (Line line in lines)
            {
                string lineContent = line.LineContent;

                //when a sectionHeader occurs, all following items are collected
                //till a new sectionHeader occurs. A new sectionHeader indicates 
                //that the old section is complete and therefore can be yielded

                if (IsSectionHeader(lineContent))
                {
                    if (items.Count > 0)
                    {
                        //adds the sectionHeader with its items
                        yield return items.ToList();
                        //clears the list for the next section
                        items.Clear();
                    }
                }

                //collect items as long as no new sectionHeader appears
                items.Add(line);
            }

            yield return items.ToList();
        }

        /// <summary>
        /// creates a new section for each sectionsGrouping element,
        /// the lineContent of the first element of a sectionsGrouping 
        /// refers to the sectionHeader, the remaining lines refer 
        /// to possible keyValuePairs
        /// </summary>
        /// <param name="sectionsGrouping">each entry contains the lines 
        /// which belong to a single section</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: a list of all sections, null otherwise</returns>
        private static IEnumerable<Section> CreateSections(
            IEnumerable<IEnumerable<Line>> sectionsGrouping,
            StreamWriter errorLogWriter)
        {
            List<Section> result = new List<Section>();

            foreach (IEnumerable<Line> lines in sectionsGrouping)
            {
                //the lineContent of the first element refers to the sectionHeader
                string sectionHeader = lines.First().LineContent;

                //the remaining lines refer to possible keyValuePairs
                IEnumerable<Line> linesOfKeyValuePairs = lines.Skip(1);

                //get all keyValuePairs
                IEnumerable<KeyValuePair<string, string>> keyValuePairs = GetKeyValuePairs
                    (linesOfKeyValuePairs, errorLogWriter);

                //check for errors
                if (keyValuePairs != null)
                {
                    result.Add
                        (new Section
                        {
                            SectionName = GetSectionHeader(sectionHeader),
                            KeyValuePairs = keyValuePairs
                        });
                }
                else
                {
                    return null;
                }
            }

            return result;
        }

        /// <summary>
        /// .) creates keyValuePairs for the given lines
        /// .) checks if all of them were created successfully 
        /// .) the number of created pairs has to be equal to the number of lines
        /// </summary>
        /// <param name="lines">contains the lines which are possibly keyValuePairs</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: a list of keyValuePairs if all of them were 
        /// created successfully, null otherwise</returns>
        private static IEnumerable<KeyValuePair<string, string>> GetKeyValuePairs(
            IEnumerable<Line> lines,
            StreamWriter errorLogWriter)
        {
            //get all successfully created keyValuePairs
            IEnumerable<KeyValuePair<string, string>> keyValuePairs =
                lines.Select(line => CreateKeyValuePair(line, errorLogWriter))
                    .Where(result => result.HasValue)
                    .Select(result => result.Value);

            //check if foreach line a keyValuePair was created 
            if (keyValuePairs.Count() == lines.Count())
            {
                return keyValuePairs;
            }

            return null;
        }

        /// <summary>
        /// .) creates a new KeyValuePair by using Regex Pattern Matching 
        /// to find the key and the value of the lineContent from the line
        /// .) checks if it is a valid keyValuePair and that the key is not empty
        /// an empty value is allowed
        /// </summary>
        /// <param name="line">contains the lineContent and its lineNumber</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: a new KeyValuePair, null otherwise</returns>
        private static Nullable<KeyValuePair<string, string>> CreateKeyValuePair(
            Line line,
            StreamWriter errorLogWriter)
        {
            Nullable<KeyValuePair<string, string>> keyValuePair = null;

            string lineContent = line.LineContent;
            int lineNumber = line.LineNumber;

            Match match = s_KeyValuePairRegex.Match(lineContent);

            //check if a match exists
            if (match.Success)
            {
                string key = GetValueFromRegexGroup(match, s_KeyGroupIndex);
                string value = GetValueFromRegexGroup(match, s_ValueGroupIndex);

                //check if the key is not empty
                if (!String.IsNullOrEmpty(key))
                {
                    keyValuePair = new KeyValuePair<string, string>(key, value);
                }
                else
                {
                    errorLogWriter.WriteLine("IniFile Error in Line " + lineNumber + " : the key is empty");
                }
            }
            else
            {
                errorLogWriter.WriteLine
                    ("IniFile Error in Line " + lineNumber + " : it is not a valid KeyValuePair");
            }

            return keyValuePair;
        }

        /// <summary>
        /// .) returns the value from the matched group 
        /// .) the group which should be taken is defined in the groupIndex
        /// </summary>
        /// <param name="match">contains the result from a single regular expression match</param>
        /// <param name="groupIndex">is the index of a matched group of the regex</param>
        /// <returns>on success: the value of the matched group, an 
        /// empty string otherwise</returns>
        private static string GetValueFromRegexGroup(
            Match match,
            int groupIndex)
        {
            Group group = match.Groups[groupIndex];
            return group.Value;
        }

        /// <summary>
        /// checks if the lineContent is a sectionHeader or not
        /// </summary>
        /// <param name="lineContent">contains the content of a line</param>
        /// <returns>true if it is a sectionheader, false otherwise</returns>
        private static bool IsSectionHeader(string lineContent)
        {
            string sectionHeader = GetSectionHeader(lineContent);
            return (!String.IsNullOrEmpty(sectionHeader));
        }

        /// <summary>
        /// if the lineContent is a sectionHeader it
        /// returns the sectionHeader without the brackets
        /// and any leading or trailing whitespaces, 
        /// otherwise an empty string will be returned
        /// </summary>
        /// <param name="lineContent">contains the content of a line</param>
        /// <returns>if a match exists: the filtered sectionHeader, 
        /// an empty string otherwise</returns>
        private static string GetSectionHeader(string lineContent)
        {
            Match match = s_SectionHeaderRegex.Match(lineContent);
            string sectionHeader = match.Groups[s_SectionHeaderGroupIndex].Value;

            return sectionHeader;
        }
    }
}
