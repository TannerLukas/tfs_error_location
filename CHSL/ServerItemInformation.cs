﻿using System;
using System.Collections.Generic;
using System.IO;
using MethodComparison;

namespace CHSL
{
    /// <summary>
    /// contains information of a serverItem (file) including the final result
    /// of method comparison for a specific serverItem.
    /// </summary>
    public class ServerItemInformation
    {
        public ServerItemInformation(string filePath)
        {
            ServerPath = filePath;
            FileName = CreateFileName(filePath);
            Changesets = new List<int>();
            Errors = new Dictionary<int, string>();
        }

        /// <summary>
        /// contains the serverpath of the item
        /// </summary>
        public string ServerPath
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the fileName of the item
        /// </summary>
        public string FileName
        {
            get;
            private set;
        }

        /// <summary>
        /// contains a list of all changesets in which the ServerItem was contained
        /// </summary>
        public List<int> Changesets
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the final result of the methodcomparison of all versions 
        /// of the serveritem.
        /// </summary>
        public MethodComparisonResult AggregatedResult
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the errors as a string for each changeset.
        /// if no errors occured then it is empty.
        /// </summary>
        public Dictionary<int, string> Errors
        {
            get;
            private set;
        }

        /// <summary>
        /// adds the new changeset with the methodcomparison result to ChangesForChangeset.
        /// if the aggregated result already contains a result then the results are merged.
        /// </summary>
        /// <param name="changesetId">the id of the new changeset</param>
        /// <param name="comparisonResult">the new result of the methodcomparison</param>
        public void AddChangesetResult(
            int changesetId,
            MethodComparisonResult comparisonResult)
        {
            Changesets.Add(changesetId);

            if (AggregatedResult == null)
            {
                //init the AggregatedResult with the first comparisonResult
                AggregatedResult = comparisonResult;
            }
            else
            {
                //if more than one result is added the results should be merged
                AggregatedResult.AggregateMethodResults(comparisonResult);
            }
        }

        /// <summary>
        /// adds all parsing errors for the given changeset to the
        /// Errors property
        /// </summary>
        /// <param name="stream">contains the errors</param>
        /// <param name="changesetId">the changset number</param>
        public void AddParsingErrors(
            MemoryStream stream,
            int changesetId)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                stream.Position = 0;
                string errors = reader.ReadToEnd();
                Errors.Add(changesetId, errors);
            }
        }

        public string GetErrorReport()
        {
            string newline = Environment.NewLine;

            string report = String.Empty;

            if (Errors != null && Errors.Count > 0)
            {
                report = "# " + FileName + newline;

                foreach (KeyValuePair<int, string> pair in Errors)
                {
                    //the value contains all errors 
                    report += "  In Changeset " + pair.Key + " : " + newline;

                    string[] errorLines = pair.Value.Split(newline.ToCharArray());

                    foreach (var errorLine in errorLines)
                    {
                        report += "  " + errorLine + newline;
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// gets the fileName of the serveritem from its path.
        /// fileName = the string after the last '/'
        /// </summary>
        /// <param name="serverPath">the path on the server for the item</param>
        /// <returns>the fileName of the serverItem</returns>
        private static string CreateFileName(string serverPath)
        {
            string[] fileNameToken = serverPath.Split('/');
            return fileNameToken[fileNameToken.Length - 1];
        }
    }
}
