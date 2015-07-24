using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        private const string s_TiaProject = "TIA";

        /// <summary>
        /// uses the Id of a serverItem as Keys. The ServerItemInformation class
        /// saves properties of the serverItem, including the results which were
        /// obtained from the AstComparer.
        /// </summary>
        private Dictionary<int, ServerItemInformation> m_ServerItems;

        private VersionControlServer m_VcServer;
        private VersionControlArtifactProvider m_ArtifactProvider;
        private WorkItemStore m_WorkItemStore;
        private MemoryStream m_ErrorLogStream;
        private StreamWriter m_ErrorLogWriter;

        private ConsoleProgressBar m_ConsoleProgressBar;
        private bool m_ShowProgressBar;

        public TfsServiceProvider(ConsoleProgressBar progressBar, bool showBar)
        {
            m_ConsoleProgressBar = progressBar;
            m_ShowProgressBar = showBar;

            m_ServerItems = new Dictionary<int, ServerItemInformation>();

            //init the tfs services which are needed
            TfsTeamProjectCollection tfsTiaProject =
                TfsTeamProjectCollectionFactory.GetTeamProjectCollection
                    (new Uri(s_TeamProjectCollectionUri));
            m_WorkItemStore = tfsTiaProject.GetService<WorkItemStore>();

            m_VcServer = tfsTiaProject.GetService<VersionControlServer>();
            m_ArtifactProvider = m_VcServer.ArtifactProvider;

            //used for errors
            m_ErrorLogStream = new MemoryStream();
            m_ErrorLogWriter = new StreamWriter(m_ErrorLogStream) {AutoFlush = true};
        }

        /// <summary>
        /// gets the Changeset specified in the changesetId.
        /// </summary>
        /// <param name="changesetId">specifies which changeset should be analyzed</param>
        /// <returns>a Dictionary which contains all ServerItemInformations <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteChangesetRequest(int changesetId)
        {
            // Get the changeset for a given Changeset Number: 
            // Changeset changeset = m_VcServer.GetChangeset(changesetId);
            // could be extended to :
            // GetChangeset(id, includeChanges, includeDownloadInfo, includeSourceRenames)
            Changeset changeset = m_VcServer.GetChangeset(changesetId, true, true, true);

            InitProgressBar(changeset.Changes.Count());

            AnalyzeChangesOfChangeset(changesetId, changeset.Changes, true);

            UpdateProgressBar(changeset.Changes.Count());

            return m_ServerItems;
        }

        /// <summary>
        /// 1) downloads the workItem specified workItemId.
        /// 2) retrieves all external links which are of type Changeset and downloads them
        /// 3) analyzes all changes of the changesets
        /// </summary>
        /// <param name="workItemId">specifies which workitem should be analyzed</param>
        /// <returns>on success: a Dictionary which contains all ServerItemInformations, 
        /// null otherwise <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteWorkItemRequest(int workItemId)
        {
            WorkItem workItem = m_WorkItemStore.GetWorkItem(workItemId);
            IEnumerable<Changeset> linkedChangesets = GetAllChangesetsOfWorkItem(workItem);

            if (linkedChangesets.Any())
            {
                int changesAmount = linkedChangesets.Sum(c => c.Changes.Count());

                InitProgressBar(changesAmount);

                int changeCounter = 0;
                foreach (Changeset changeset in linkedChangesets)
                {
                    GetChangeSetInformationByChangeset(changeset);
                    changeCounter += changeset.Changes.Count();
                    UpdateProgressBar(changeCounter);
                }

                return m_ServerItems;
            }
            else
            {
                m_ErrorLogWriter.WriteLine
                    ("The workItem: " + workItemId + " does not contain any linked changesets.");
                return null;
            }
        }

        /// <summary>
        /// retrieves and analyzes all workItems which are returned by the execution of the query
        /// </summary>
        /// <param name="text">contains the text of a query (select ...)</param>
        /// <returns>on success: a Dictionary which contains all ServerItemInformations, 
        /// null otherwise <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteQueryString(string text)
        {
            WorkItemCollection workItems = RunQueryForWorkItems(text);

            if (workItems.Count > 0)
            {
                AnalyzeWorkItems(ref workItems);
            }
            else
            {
                m_ErrorLogWriter.WriteLine("No WorkItems where found.");
                return null;
            }

            return m_ServerItems;
        }

        /// <summary>
        /// retrieves and analyzes all workItems to which the person(name) is assigned to
        /// </summary>
        /// <param name="name">the name of the person</param>
        /// <returns>on success: a Dictionary which contains all ServerItemInformations, 
        /// null otherwise <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteQueryForPerson(string name)
        {
            //create the query
            string query = "select * from WorkItems where [System.AssignedTo] = '" + name +
                           "' AND [System.ExternalLinkCount] > 0";

            WorkItemCollection workItems = RunQueryForWorkItems(query);

            if (workItems.Count > 0)
            {
                AnalyzeWorkItems(ref workItems);
            }
            else
            {
                m_ErrorLogWriter.WriteLine("No WorkItems where found for: " + name);
                return null;
            }

            return m_ServerItems;
        }

        /// <summary>
        /// 1) tries to find the queryName in a subfolder of the TIA project.
        /// 2) if it was found: it is executed and analyzed, otherwise an error message is shown.
        /// Note that if multiple queries match the requested queryName the first will be used.
        /// </summary>
        /// <param name="queryName">the name of a query (typically a shared query),
        /// but it also works with "My Tasks"</param>
        /// <returns>on success: a Dictionary which contains all ServerItemInformations, 
        /// null otherwise <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteQueryByName(string queryName)
        {
            Project teamProject = m_WorkItemStore.Projects[s_TiaProject];
            QueryHierarchy hierarchy = teamProject.QueryHierarchy;

            Guid queryId = FindQuery(hierarchy, queryName);

            if (queryId.Equals(Guid.Empty))
            {
                m_ErrorLogWriter.WriteLine("The query:" + queryName + " was not found");
                return null;
            }

            WorkItemCollection workItemsCollection = RunQueryOfGuid(queryId);

            if (workItemsCollection.Count > 0)
            {
                //List<WorkItem> workItems = ConvertWorkItemCollectionToList(ref workItemsCollection);
                //IEnumerable<WorkItem> filteredWorkItems = workItems.Where(w => w.ExternalLinkCount > 0);

                AnalyzeWorkItems(ref workItemsCollection);
            }
            else
            {
                m_ErrorLogWriter.WriteLine("No WorkItems where found for: " + queryName);
                return null;
            }

            return m_ServerItems;
        }

        public Dictionary<int, ServerItemInformation> ExecuteQueryByGuId(string guid)
        {
            WorkItemCollection workItems = RunQueryOfGuid(new Guid(guid));

            if (workItems.Count > 0)
            {
                AnalyzeWorkItems(ref workItems);
            }
            else
            {
                m_ErrorLogWriter.WriteLine("No WorkItems where found for: " + guid);
                return null;
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
                ExecuteChangesetRequest(changeSet);
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
                Console.WriteLine();
                Console.WriteLine(errors);
            }
        }

        /// <summary>
        /// analyzes all changesets which are linked to a workItem in the collection.
        /// </summary>
        /// <param name="workItems">contains all workItems</param>
        private void AnalyzeWorkItems(ref WorkItemCollection workItems)
        {
            List<int> processedChangesets = new List<int>();

            InitProgressBar(workItems.Count);
            
            foreach (WorkItem item in workItems)
            {
                List<Changeset> linkedChangesets = GetAllChangesetsOfWorkItem(item);

                foreach (Changeset changeset in linkedChangesets)
                {
                    int changesetId = changeset.ChangesetId;
                    if (processedChangesets.Contains(changesetId))
                    {
                        //skip the changeset, because it was already processed
                        continue;
                    }

                    GetChangeSetInformationByChangeset(changeset, false);
                    processedChangesets.Add(changesetId);
                }

                UpdateProgressBar();
            }
        }

        /// <summary>
        /// searches for a query specified by its name
        /// </summary>
        /// <param name="folder">a folder that my contain QueryDefinitions and other QueryFolders.</param>
        /// <param name="queryName">contains the name of a query</param>
        /// <returns>on success: the GUID of the query, Guid.Empty otherwise</returns>
        private static Guid FindQuery(
            IEnumerable<QueryItem> folder,
            string queryName)
        {
            foreach (QueryItem item in folder)
            {
                if (item.Name.Equals(queryName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return item.Id;
                }

                QueryFolder itemFolder = item as QueryFolder;
                if (itemFolder != null)
                {
                    Guid result = FindQuery(itemFolder, queryName);
                    if (!result.Equals(Guid.Empty))
                    {
                        return result;
                    }
                }
            }

            return Guid.Empty;
        }

        /// <summary>
        /// retrieves the id of the changeset and analyzes all changes
        /// </summary>
        /// <param name="changeset">contains a tfs changeset</param>
        /// <param name="updateProgressBar">a flag which indicates if the progressBar 
        /// should be updated. it should be set to false only when working on a query</param>
        private void GetChangeSetInformationByChangeset(Changeset changeset, bool updateProgressBar = true)
        {
            int changesetId = changeset.ChangesetId;
            AnalyzeChangesOfChangeset(changesetId, changeset.Changes, updateProgressBar);
        }

        /// <summary>
        /// Retrieves all changesets of a workitem.
        /// </summary>
        /// <param name="item">contains the workItem information</param>
        /// <returns>a list of all associated changesets of a workitem</returns>
        private List<Changeset> GetAllChangesetsOfWorkItem(WorkItem item)
        {
            List<Changeset> result = new List<Changeset>();

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
        /// <param name="updateProgressBar">a flag which indicates if the progressBar 
        /// should be updated. it should be set to false only when working on a query</param>
        private void AnalyzeChangesOfChangeset(
            int changesetId,
            IEnumerable<Change> changes,
            bool updateProgressBar)
        {
            //only work with c# code files and skip AssemblyInfo file
            IEnumerable<Change> relevantChanges = changes.Where
                (c =>
                    c.Item.ServerItem.EndsWith(s_CodeFileSuffix) &&
                    !c.Item.ServerItem.EndsWith(s_AssemblyInfo));

            foreach (Change change in relevantChanges)
            {
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
                string serverItemPath = change.Item.ServerItem;
                ServerItemInformation serverItem = GetOrCreateServerItemInformation
                    (itemId, serverItemPath);

                if (change.ChangeType.HasFlag(ChangeType.Delete))
                {
                    serverItem.IsDeleted = true;
                }

                //start with the method comparison
                using (MemoryStream errorStream = new MemoryStream())
                {
                    MethodComparisonResult methodComparison = AstComparer.CompareSyntaxTrees
                        (oldFileContent, serverItem.FileName, newFileContent, serverItem.FileName,
                            errorStream, true, true, true);

                    if (methodComparison != null)
                    {
                        //no errors occured: add the obtained result to the serverItem
                        serverItem.AddChangesetResult(changesetId, methodComparison);
                    }
                    else
                    {
                        //a parsing error occured.
                        serverItem.AddParsingErrors(errorStream, changesetId);
                    }
                }

                if (updateProgressBar)
                {
                    UpdateProgressBar();
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
            //return m_VcServer.GetItem(itemId, changesetId, GetItemsOptions.Download);
            return m_VcServer.GetItem(itemId, changesetId);
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

        /// <summary>
        /// converts a workItemCollection to a list.
        /// </summary>
        /// <param name="collection">the collection which should be converted</param>
        /// <returns>a list of all workItems in the workItemCollection</returns>
        private List<WorkItem> ConvertWorkItemCollectionToList(ref WorkItemCollection collection)
        {
            List<WorkItem> itemsList = (from WorkItem item in collection select item).ToList();

            return itemsList;
        }

        /// <summary>
        /// Retrieves and execeutes the query which is defined via the guid.
        /// </summary>
        /// <param name="guid">containts the guid of the query</param>
        /// <returns>a collection of workitems which are returned by the query</returns>
        private WorkItemCollection RunQueryOfGuid(Guid guid)
        {
            QueryDefinition definition = m_WorkItemStore.GetQueryDefinition(guid);
            WorkItemCollection workItems = RunQueryForWorkItems(definition.QueryText);

            return workItems;
        }

        /// <summary>
        /// runs the query defined by text againts the WorkItemStore
        /// </summary>
        /// <param name="text">contains the text of the query which should be executed.</param>
        /// <returns>a collection of workitems which are returned by the query</returns>
        private WorkItemCollection RunQueryForWorkItems(string text)
        {
            return m_WorkItemStore.Query(text);
        }

        /// <summary>
        /// inits the progress bar. 
        /// </summary>
        /// <param name="maxVal">contains the maxValue of the progress bar</param>
        private void InitProgressBar(int maxVal)
        {
            if (m_ShowProgressBar && m_ConsoleProgressBar != null)
            {
                m_ConsoleProgressBar.SetMaxVal(maxVal);
                m_ConsoleProgressBar.SetLoadFinished();
            }
        }

        /// <summary>
        /// updates the value of the progress bar
        /// </summary>
        /// <param name="value">optional: the value to which the progress bar should be set</param>
        private void UpdateProgressBar(int value = 0)
        {
            if (m_ShowProgressBar && m_ConsoleProgressBar != null)
            {
                m_ConsoleProgressBar.RefreshBar(value);
            }
        }
    }
}
