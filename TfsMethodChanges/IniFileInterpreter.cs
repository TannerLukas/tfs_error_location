using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TfsMethodChanges
{
    /// <summary>
    /// interprets all sections of an iniFile
    /// </summary>
    internal class IniFileInterpreter
    {
        private const string s_ServerSection = "SERVER";
        private const string s_ServerPathKey = "SERVERPATH";
        private const string s_ProjectKey = "PROJECT";

        private const string s_UniqueKeyError =
            "IniFile Error: Each Key in a section is only allowed once.";

        private const string s_RequiredPairsError =
            "IniFile Error: Please define KeyValuePairs for " + s_ServerPathKey + " and " +
            s_ProjectKey + ".";

        private const string s_RequiredSectionError =
            "IniFile Error: Please define a " + s_ServerSection + " section.";

        /// <summary>
        /// define all sections which are allowed in the IniFile
        /// </summary>
        private static readonly string[] s_AllowedSections = {s_ServerSection};

        /// <summary>
        /// define all keys which are allowed in the IniFile
        /// </summary>
        private static readonly string[] s_AllowedKeys = {s_ServerPathKey, s_ProjectKey};

        /// <summary>
        /// should be used for case insensitive comparisons
        ///  </summary>
        private const StringComparison s_StandardStringComparison =
            StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// string comparer which ignores the case of strings
        /// should be used for comparisons with AllowedSections and -Keys
        /// </summary>
        private static readonly StringComparer s_AllowedStringComparer =
            StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// creates a TfsConfiguration Object which contains all essential information
        /// which is used to connect to a TfsServer
        /// </summary>
        /// <param name="sections">all sections of the iniFile with its corresponding 
        /// keyValuePairs</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: a TfsConfiguration Object, null otherwise</returns>
        public static TfsConfiguration InterpretIniFile(
            IEnumerable<Section> sections,
            StreamWriter errorLogWriter)
        {
            List<Section> sectionsList = sections.ToList();

            if (CheckSectionsForErrors(sectionsList, errorLogWriter))
            {
                //get all serverSections
                IEnumerable<Section> serverSections = sectionsList.Where
                    (s => EqualsComparison(s.SectionName, s_ServerSection));

                if (serverSections.Any())
                {
                    //if there are multiple serverSections defined only the first will be used
                    return CreateTfsConfiguration(serverSections.First(), errorLogWriter);
                }
                else
                {
                    errorLogWriter.WriteLine(s_RequiredSectionError);
                }
            }

            return null;
        }

        /// <summary>
        /// extracts the information which is need for the configuration of the tfsServer
        /// from the given section
        /// </summary>
        /// <param name="section">contains a [Server] section</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>on success: a TfsConfiguration Object, null otherwise</returns>
        private static TfsConfiguration CreateTfsConfiguration(
            Section section,
            StreamWriter errorLogWriter)
        {
            string serverPath = GetValueOfKeyValuePairs(section.KeyValuePairs, s_ServerPathKey);
            string project = GetValueOfKeyValuePairs(section.KeyValuePairs, s_ProjectKey);

            if (serverPath != null &&
                project != null)
            {
                TfsConfiguration conf = new TfsConfiguration(serverPath, project);
                return conf;
            }

            errorLogWriter.WriteLine(s_RequiredPairsError);
            return null;
        }

        /// <summary>
        /// retrieves the value of a keyValuePair defined by its key.
        /// a key which does not exist exactly once indicates an error.
        /// </summary>
        /// <param name="pairs">contains several keyValuePairs</param>
        /// <param name="key">the key to search for</param>
        /// <returns>on success: the value of a keyValuePair defined by the key, null otherwise</returns>
        private static string GetValueOfKeyValuePairs(
            IEnumerable<KeyValuePair<string, string>> pairs,
            string key)
        {
            IEnumerable<KeyValuePair<string, string>> keyPairs = pairs.Where
                (p => EqualsComparison(p.Key, key));

            if (keyPairs.Count() != 1)
            {
                return null;
            }
            else
            {
                string value = keyPairs.First().Value;
                return value;
            }
        }

        /// <summary>
        /// checks if all sections are correctly defined
        /// </summary>
        /// <param name="sections">all sections of an iniFile</param>
        /// <param name="errorLogWriter">StreamWriter used for errors</param>
        /// <returns>true if all sections were defined correctly, false otherwise</returns>
        private static bool CheckSectionsForErrors(
            IEnumerable<Section> sections,
            StreamWriter errorLogWriter)
        {
            return (AreAllSectionsAndKeysAllowed(sections, errorLogWriter) &&
                    CheckEachKeyOnlyExistsOnce(sections, errorLogWriter));
        }

        /// <summary>
        /// checks if each key in a section exists exactly once
        /// </summary>
        /// <param name="sections">all sections of an iniFile</param>
        /// <param name="errorLogWriter">StreamWriter used for errors</param>
        /// <returns>true: if all keys in a section exist exactly once, false otherwise</returns>
        private static bool CheckEachKeyOnlyExistsOnce(
            IEnumerable<Section> sections,
            StreamWriter errorLogWriter)
        {
            foreach (Section section in sections)
            {
                //group the keys in order the check if a specific key exists multiple times
                IEnumerable<string> keysWithMultipleOccurence =
                    section.KeyValuePairs.GroupBy(p => p.Key)
                        .Where(p => p.Count() > 1)
                        .Select(p => p.Key);

                if (keysWithMultipleOccurence.Any())
                {
                    errorLogWriter.WriteLine(s_UniqueKeyError);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// the string equals function which should be used to compare two strings
        /// </summary>
        /// <param name="first">the first string</param>
        /// <param name="second">the second string</param>
        /// <returns>true if the strings are equal, false otherwise</returns>
        private static bool EqualsComparison(
            string first,
            string second)
        {
            return first.Equals(second, s_StandardStringComparison);
        }

        /// <summary>
        /// 1) loops over sections and checks whether the section names & keys are allowed or not
        /// 2) prints an error message for the sections and keys which are not allowed
        /// </summary>
        /// <param name="sections">all sections of the iniFile with its corresponding 
        /// keyValuePairs</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>true if no errors occurred, false otherwise</returns>
        private static bool AreAllSectionsAndKeysAllowed(
            IEnumerable<Section> sections,
            StreamWriter errorLogWriter)
        {
            //convert it to a list so that the enumeration is only done once
            List<Section> sectionsList = sections.ToList();

            //get all sectionNames which are not allowed
            IEnumerable<string> notAllowedSections =
                sectionsList.Where
                    (section =>
                        !s_AllowedSections.Contains(section.SectionName, s_AllowedStringComparer))
                    .Select(s => s.SectionName);

            //print not allowed sectionNames
            foreach (string sectionName in notAllowedSections)
            {
                errorLogWriter.WriteLine
                    ("Error: '" + sectionName + "' is not an allowed section Name");
            }

            //get all keys which are not allowed
            IEnumerable<string> notAllowedKeys =
                sectionsList.SelectMany(section => section.KeyValuePairs)
                    .Where(pairs => !s_AllowedKeys.Contains(pairs.Key, s_AllowedStringComparer))
                    .Select(s => s.Key);

            foreach (string key in notAllowedKeys)
            {
                errorLogWriter.WriteLine("Error: '" + key + "' is not an allowed key Name");
            }

            return ((notAllowedSections.Count() + notAllowedKeys.Count()) == 0);
        }
    }
}
