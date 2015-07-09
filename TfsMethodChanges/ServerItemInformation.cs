using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tfs_Error_Location
{
    /// <summary>
    /// contains information of a serverItem (file) including the final result
    /// of method comparison for a specific serverItem.
    /// </summary>
    public class ServerItemInformation
    {
        public ServerItemInformation(
            string filePath)
        {
            ServerPath = filePath;
            FileName = CreateFileName(filePath);
            ChangesForChangeset = new Dictionary<int, MethodComparisonResult>();
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
        /// contains the result of the methodcomparison for each changeset.
        /// This is used in order to track which methods were changed in
        /// which changeset.
        /// </summary>
        public Dictionary<int, MethodComparisonResult> ChangesForChangeset
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
        /// adds the new changeset with the methodcomparison result to ChangesForChangeset.
        /// if the aggregated result already contains a result then the results are merged.
        /// </summary>
        /// <param name="changesetId">the id of the new changeset</param>
        /// <param name="comparisonResult">the new result of the methodcomparison</param>
        public void AddChangesetResult(
            int changesetId,
            MethodComparisonResult comparisonResult)
        {
            if (ChangesForChangeset == null)
            {
                ChangesForChangeset = new Dictionary<int, MethodComparisonResult>();
            }
            
            ChangesForChangeset.Add(changesetId, comparisonResult);

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
        /// gets the fileName of the serveritem from its path.
        /// fileName = the string after the last '/'
        /// </summary>
        /// <param name="serverPath">the path on the server for the item</param>
        /// <returns>the fileName of the serverItem</returns>
        private static string CreateFileName(string serverPath)
        {
            string[] fileNameToken = serverPath.Split('/');
            return fileNameToken[fileNameToken.Length-1];         
        }
    }
}
