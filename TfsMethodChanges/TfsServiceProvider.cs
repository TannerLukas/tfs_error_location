using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Tfs_Error_Location;

namespace TfsMethodChanges
{
    /// <summary>
    /// provides the tfs services which are needed in order to download 
    /// changesets/workitems and execute a method comparison on their items.
    /// </summary>
    public class TfsServiceProvider
    {
        private const string s_TeamProjectCollectionUri =
            "https://iiaastfs.ww004.siemens.net/tfs/TIA";

        private const string s_CodeFileSuffix = ".cs";
        private const string s_AssemblyInfo = "AssemblyInfo.cs";
        private const string s_Changeset = "Changeset";

        /// <summary>
        /// uses the Id of a serverItem as Keys. The ServerItemInformation class
        /// saves properties of the serverItem, including the results which were
        /// obtained from the AstComparer.
        /// </summary>
        private Dictionary<int, ServerItemInformation> m_ServerItems;

        private VersionControlServer m_VcServer;
        private TfsTeamProjectCollection m_TfsTiaProject;
        private VersionControlArtifactProvider m_ArtifactProvider;
        private MemoryStream m_ErrorLogStream;
        private StreamWriter m_ErrorLogWriter;

        public TfsServiceProvider()
        {
            m_ServerItems = new Dictionary<int, ServerItemInformation>();
            m_TfsTiaProject = TfsTeamProjectCollectionFactory.GetTeamProjectCollection
                (new Uri(s_TeamProjectCollectionUri));
            m_VcServer = m_TfsTiaProject.GetService<VersionControlServer>();
            m_ArtifactProvider = m_VcServer.ArtifactProvider;
            m_ErrorLogStream = new MemoryStream();
            m_ErrorLogWriter = new StreamWriter(m_ErrorLogStream) {AutoFlush = true};
        }

        /// <summary>
        /// gets the Changeset specified in the changesetId.
        /// </summary>
        /// <param name="changesetId">specifies which changeset should be analyzed</param>
        /// <returns>a Dictionary which contains all ServerItemInformations <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> GetChangeSetInformationById(int changesetId)
        {
            // Get the changeset for a given Changeset Number: 
            // Changeset changeset = m_VcServer.GetChangeset(changesetId);
            // could be extended to :
            // GetChangeset(id, includeChanges, includeDownloadInfo, includeSourceRenames)
            Changeset changeset = m_VcServer.GetChangeset(changesetId, true, true, true);
            
            AnalyzeChangesOfChangeset(changesetId, changeset.Changes);
            return m_ServerItems;
        }

        /// <summary>
        /// 1) downloads the workItem specified workItemId.2
        /// 2) retrieves all external links which are of type Changeset and downloads them
        /// 3) analyzes all changes of the changesets
        /// </summary>
        /// <param name="workItemId">specifies which workitem should be analyzed</param>
        /// <returns>a Dictionary which contains all ServerItemInformations <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> GetWorkItemInformations(int workItemId)
        {
            WorkItemStore workItemStore = m_TfsTiaProject.GetService<WorkItemStore>();
            WorkItem workItem = workItemStore.GetWorkItem(workItemId);
            IEnumerable<Changeset> linkedChangesets = GetAllChangesetsOfWorkItem(workItem);

            if (linkedChangesets != null)
            {
                foreach (Changeset changeset in linkedChangesets)
                {
                    GetChangeSetInformationByChangeset(changeset);
                }

                return m_ServerItems;
            }

            return null;
        }

        /// <summary>
        /// analyzes the changes of a a list of changesets. 
        /// used only for test purposes.
        /// </summary>
        /// <param name="changeSets">a list of changesetIds</param>
        /// <returns>a Dictionary which contains all ServerItemInformations <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> GetResultForChangesets(
            IEnumerable<int> changeSets)
        {
            foreach (int changeSet in changeSets)
            {
                GetChangeSetInformationById(changeSet);
            }

            return m_ServerItems;
        }

        /// <summary>
        /// prints the errors to the console
        /// </summary>
        public void PrintErrorReport()
        {
            using (StreamReader reader = new StreamReader(m_ErrorLogStream))
            {
                m_ErrorLogStream.Position = 0;
                string errors = reader.ReadToEnd();
                Console.WriteLine(errors);
            }
        }

        /// <summary>
        /// retrieves the id of the changeset and analyzes all changes
        /// </summary>
        /// <param name="changeset">contains a tfs changeset</param>
        private void GetChangeSetInformationByChangeset(Changeset changeset)
        {
            int changesetId = changeset.ChangesetId;
            AnalyzeChangesOfChangeset(changesetId, changeset.Changes);
        }

        /// <summary>
        /// Retrieves all changesets of a workitem.
        /// </summary>
        /// <param name="item">contains the workItem information</param>
        /// <returns>a list of all associated changesets of a workitem</returns>
        private IEnumerable<Changeset> GetAllChangesetsOfWorkItem(WorkItem item)
        {
            List<Changeset> result = new List<Changeset>();

            try
            {
                //a workitem has a list of external links, which can be of different types
                //the retrieve only the changesets, the ArtifactType of a link has to 
                //be of type "Changeset"
                foreach (ExternalLink link in item.Links.OfType<ExternalLink>())
                {
                    ArtifactId artifact = LinkingUtilities.DecodeUri(link.LinkedArtifactUri);
                    if (String.Equals(artifact.ArtifactType, s_Changeset, StringComparison.Ordinal))
                    {
                        // Convert the artifact URI to Changeset object.
                        result.Add(m_ArtifactProvider.GetChangeset(new Uri(link.LinkedArtifactUri)));
                    }
                }
            }
            catch (UriFormatException exception)
            {
                m_ErrorLogWriter.WriteLine(exception.Message);
                return null;
            }

            return result;
        }

        /// <summary>
        /// for each change in the changeset:
        /// 1) downloads the file in the changeset and the previous version.
        /// 2) compares their methods
        /// 3) if a serverItemInformation with the same Id was already created
        /// -> merge their results, else: create a new ServerItemInformation Object
        /// and add it to result.
        /// </summary>
        /// <param name="changesetId">specifies which changeset is used</param>
        /// <param name="changes">contains all changes related to a changeset</param>
        /// <param name="workItemId">optional parameter: if it contains a workItemId, 
        /// the workitemId should be added to the serverItem.</param>
        private void AnalyzeChangesOfChangeset(
            int changesetId,
            IEnumerable<Change> changes,
            int workItemId = 0)
        {
            foreach (Change change in changes)
            {
                string serverItemPath = change.Item.ServerItem;

                //only work with c# code files
                if (!serverItemPath.EndsWith(s_CodeFileSuffix) ||
                    serverItemPath.EndsWith(s_AssemblyInfo))
                {
                    continue;
                }

                int itemId = change.Item.ItemId;

                Item currentItem = GetServerItem(itemId, changesetId);

                if (currentItem == null)
                {
                    continue;
                }

                //with id - 1 the previous version of a file is returned
                Item previousItem = GetServerItem(itemId, changesetId - 1);

                //If no previous item is found, it is assumed that the currentItem was added in 
                //this changeset. The content of the previous item will be declared with an empty
                //string. The AstComparer will then mark all methods as added.
                string oldFileContent = String.Empty;

                if (previousItem != null)
                {
                    oldFileContent = GetFileString(previousItem);
                }

                string newFileContent = GetFileString(currentItem);

                //if a serverItem with the itemId already exists then the methodcomparison result
                //is aggregated, otherwise a new serverItem is created
                ServerItemInformation serverItem = GetOrCreateServerItemInformation
                    (itemId, serverItemPath);

                if (change.ChangeType.HasFlag(ChangeType.Delete))
                {
                    serverItem.SetDeletedState(true);
                }

                //start with the method comparison
                using (MemoryStream errorStream = new MemoryStream())
                {
                    MethodComparisonResult methodComparison = AstComparer.CompareSyntaxTrees
                        (oldFileContent, serverItem.FileName, newFileContent, serverItem.FileName,
                            errorStream, true);

                    if (methodComparison != null)
                    {
                        //no errors occured: add the obtained result to the serverItem
                        serverItem.AddChangesetResult(changesetId, workItemId, methodComparison);
                    }
                    else
                    {
                        //a parsing error occured.
                        serverItem.AddParsingErrors(errorStream, changesetId);
                    }
                }
            }
        }

        /// <summary>
        /// creates a new serverItem if an item with the specified itemId does not 
        /// already exist in m_ServerItems. otherwise, the item with the itemId will
        /// be returned.
        /// </summary>
        /// <param name="itemId">specified the id of a serverItem</param>
        /// <param name="serverItemPath">contains the serverpath of a serverItem</param>
        /// <returns>returns a a newly created ServerItem if does not already 
        /// exist, the existing item otherwise</returns>
        private ServerItemInformation GetOrCreateServerItemInformation(
            int itemId,
            string serverItemPath)
        {
            ServerItemInformation serverItem;

            if (!m_ServerItems.TryGetValue(itemId, out serverItem))
            {
                serverItem = new ServerItemInformation(serverItemPath);
                m_ServerItems.Add(itemId, serverItem);
            }

            return serverItem;
        }

        /// <summary>
        /// Gets an Item from the repository, based on itemId, changesetNumber, and options.
        /// </summary>
        /// <param name="itemId">used to identify the item</param>
        /// <param name="changesetId">specifies which version of the file should be returned</param>
        /// <returns>The specified Item. Null if not found.</returns>
        private Item GetServerItem(
            int itemId,
            int changesetId)
        {
            return m_VcServer.GetItem(itemId, changesetId, GetItemsOptions.Download);
        }

        /// <summary>
        /// downloads the content of a item and returns it as a string.
        /// </summary>
        /// <param name="item">the item which should be downloaded</param>
        /// <returns>the contents of the item as a string</returns>
        private static string GetFileString(Item item)
        {
            // Setup string container
            string content;

            // Download file into stream
            using (Stream stream = item.DownloadFile())
            {
                // Use MemoryStream to copy downloaded Stream
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);

                    // Use StreamReader to read MemoryStream created from byte array
                    using (
                        StreamReader streamReader = new StreamReader
                            (new MemoryStream(memoryStream.ToArray())))
                    {
                        content = streamReader.ReadToEnd();
                    }
                }
            }

            // return string
            return content;
        }
    }
}
