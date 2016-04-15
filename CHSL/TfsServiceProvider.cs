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
using MethodComparerison;
using MethodComparer = MethodComparerison.MethodComparer;

namespace CHSL
{
    /// <summary>
    /// provides the tfs services which are needed in order to download 
    /// changesets/workitems and execute a method comparison on their items.
    /// </summary>
    internal class TfsServiceProvider
    {
        private const string s_CodeFileSuffix = ".cs";
        private const string s_AssemblyInfo = "AssemblyInfo.cs";
        private const string s_Changeset = "Changeset";
        private const int s_ExpectedMinTokens = 2;

        /// <summary>
        /// uses the Id of a serverItem as Keys. The ServerItemInformation class
        /// saves properties of the serverItem, including the results which were
        /// obtained from the AstComparer.
        /// </summary>
        private readonly Dictionary<int, ServerItemInformation> m_ServerItems;

        private readonly VersionControlServer m_VcServer;
        private readonly VersionControlArtifactProvider m_ArtifactProvider;
        private readonly WorkItemStore m_WorkItemStore;
        private readonly MemoryStream m_ErrorLogStream;
        private readonly StreamWriter m_ErrorLogWriter;

        private readonly ConsoleProgressBar m_ConsoleProgressBar;
        private readonly bool m_ShowProgressBar;

        public TfsServiceProvider(
            TfsConfiguration config,
            ConsoleProgressBar progressBar,
            bool showBar)
        {
            m_ServerItems = new Dictionary<int, ServerItemInformation>();
            m_ConsoleProgressBar = progressBar;
            m_ShowProgressBar = showBar;

            //init the tfs services which are needed
            string teamProjectCollectionUri = GetTfsConfigurationInformation(config);

            UICredentialsProvider provider = new UICredentialsProvider();

            TfsTeamProjectCollection project =
                TfsTeamProjectCollectionFactory.GetTeamProjectCollection
                    (new Uri(teamProjectCollectionUri), provider);

            project.Authenticate();

            m_WorkItemStore = project.GetService<WorkItemStore>();

            m_VcServer = project.GetService<VersionControlServer>();
            m_ArtifactProvider = m_VcServer.ArtifactProvider;

            //used for errors
            m_ErrorLogStream = new MemoryStream();
            m_ErrorLogWriter = new StreamWriter(m_ErrorLogStream) {AutoFlush = true};
        }

        /// <summary>
        /// extracts the tfs configuration from the config parameter which should be used     
        /// </summary>
        /// <param name="config">contains the tfsConfiguration which should be used</param>
        /// <returns>a string containing the definition of the teamProjectCollection</returns>
        private static string GetTfsConfigurationInformation(TfsConfiguration config)
        {
            string seperatorChar = Path.AltDirectorySeparatorChar + String.Empty;
            string server = config.Server;
            string project = config.Project;

            string teamProjectCollection;
            if (config.Server.EndsWith(seperatorChar))
            {
                teamProjectCollection = server + project;
            }
            else
            {
                teamProjectCollection = server + seperatorChar + project;
            }

            return teamProjectCollection;
        }

        /// <summary>
        /// gets the Changeset specified in the changesetId.
        /// </summary>
        /// <param name="changesetId">specifies which changeset should be analyzed</param>
        /// <returns>a Dictionary which contains all ServerItemInformations <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteChangesetRequest(int changesetId)
        {
            try
            {
                // Get the changeset for a given Changeset Number: 
                // GetChangeset(id, [includeChanges, includeDownloadInfo, includeSourceRenames])
                Changeset changeset = m_VcServer.GetChangeset(changesetId, true, true, false);

                int changeCounter = changeset.Changes.Count();
                string message = String.Format("Start to work on {0} changes.", changeCounter);
                InitProgressBar(changeCounter, message);

                AnalyzeChangesOfChangeset(changesetId, changeset.Changes, true);

                UpdateProgressBar(changeset.Changes.Count());
            }
            catch (VersionControlException exception)
            {
                m_ErrorLogWriter.WriteLine("\r\n" + exception.Message);
                return null;
            }

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

                //init hte 
                int changesetCounter = linkedChangesets.Count();
                string message = String.Format
                    ("Start to work on {0} changesets with {1} changes.", changesetCounter,
                        changesAmount);
                InitProgressBar(changesAmount, message);

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
        /// <param name="queryPath">the name of a query (typically a shared query),
        /// but it also works with "My Tasks"</param>
        /// <returns>on success: a Dictionary which contains all ServerItemInformations, 
        /// null otherwise <see cref="m_ServerItems"/></returns>
        public Dictionary<int, ServerItemInformation> ExecuteQueryByName(string queryPath)
        {
            //split the queryPath in all path tokens
            string[] pathTokens = queryPath.Split
                (new char[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar});

            //filter the empty tokens
            IEnumerable<string> filteredPathTokens = pathTokens.Where(p => !p.Equals(String.Empty));

            if (filteredPathTokens.Count() < s_ExpectedMinTokens)
            {
                m_ErrorLogWriter.WriteLine
                    ("Please insert a valid queryName: project/{path}/queryname.");
                return null;
            }

            /*
            //the first part of the path refers to the projectName
            string project = pathTokens.First();

            Project teamProject = m_WorkItemStore.Projects[project];
            QueryHierarchy hierarchy = teamProject.QueryHierarchy;
            
            //traverse through the folders and find the query
            QueryDefinition query = FindQueryByPathComparison(hierarchy, queryPath);
             * */
             
            QueryDefinition query = FindQueryByFolderStructure(filteredPathTokens);

            if (query == null)
            {
                m_ErrorLogWriter.WriteLine
                    ("The query: " + queryPath +
                     " was not found or does not refer to a queryDefinition.");
                return null;
            }

            WorkItemCollection workItems = RunQueryForWorkItems(query.QueryText);

            if (workItems.Count > 0)
            {
                //List<WorkItem> workItems = ConvertWorkItemCollectionToList(ref workItemsCollection);
                //IEnumerable<WorkItem> filteredWorkItems = workItems.Where(w => w.ExternalLinkCount > 0);

                AnalyzeWorkItems(ref workItems);
            }
            else
            {
                m_ErrorLogWriter.WriteLine("No WorkItems where found for: " + queryPath);
                return null;
            }

            return m_ServerItems;
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

                if (!errors.Equals(String.Empty))
                {
                    Console.WriteLine("\r\n\r\nERRORS: ");
                    Console.WriteLine(errors);
                }
            }
        }

        /// <summary>
        /// analyzes all changesets which are linked to a workItem in the collection.
        /// </summary>
        /// <param name="workItems">contains all workItems</param>
        private void AnalyzeWorkItems(ref WorkItemCollection workItems)
        {
            List<int> processedChangesets = new List<int>();

            int workItemCounter = workItems.Count;
            string message = String.Format("Start to work on {0} workitems.", workItemCounter);
            InitProgressBar(workItemCounter, message);

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

        private QueryDefinition FindQueryByFolderStructure(IEnumerable<string> pathTokens)
        {
            string project = pathTokens.First();
            Project teamProject = m_WorkItemStore.Projects[project];
            QueryHierarchy hierarchy = teamProject.QueryHierarchy;

            //skip the first element because it refers to the project definition
            IEnumerable<string> remainingTokens = pathTokens.Skip(1);

            QueryDefinition queryDefinition = TraverseQueryFolders(remainingTokens, hierarchy);

            return queryDefinition;
        }

        private static QueryDefinition TraverseQueryFolders(
            IEnumerable<string> pathTokens,
            QueryHierarchy hierarchy)
        {
            //the number of pathtokens is >= 1

            List<string> pathTokensList = pathTokens.ToList();

            //if count == 1, the pathtokens contain only a QueryDefinition
            //if counter > 1 the pathtokens contains some subfolders

            QueryDefinition query = null;

            if (pathTokensList.Count == 1)
            {
                QueryItem item = hierarchy[pathTokensList.First()];
                query = item as QueryDefinition;
            }
            else if (pathTokensList.Count > 1)
            {
                string queryName = pathTokensList.Last();

                QueryFolder folder = hierarchy;
                for (int i = 0; i < pathTokensList.Count() - 1; i++)
                {
                    QueryItem item = folder[pathTokensList[i]];
                    folder = item as QueryFolder;

                    if (folder == null)
                    {
                        return null;
                    }
                }

                QueryItem queryItem = folder[queryName];
                query = queryItem as QueryDefinition;
            }

            return query;
        }

        /// <summary>
        /// searches for a query specified by its path/name
        /// </summary>
        /// <param name="folder">a folder that my contain QueryDefinitions and other QueryFolders.</param>
        /// <param name="queryPath">contains the whole path of a query</param>
        /// <returns>on success: the QueryDefinition of the query, null otherwise</returns>
        private static QueryDefinition FindQueryByPathComparison(
            IEnumerable<QueryItem> folder,
            string queryPath)
        {
            foreach (QueryItem item in folder)
            {
                if (item.Path.Equals(queryPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    QueryDefinition queryDefinition = item as QueryDefinition;
                    return queryDefinition;
                }

                QueryFolder itemFolder = item as QueryFolder;

                if (itemFolder != null)
                {
                    QueryDefinition result = FindQueryByPathComparison(itemFolder, queryPath);

                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// retrieves the id of the changeset and analyzes all changes
        /// </summary>
        /// <param name="changeset">contains a tfs changeset</param>
        /// <param name="updateProgressBar">a flag which indicates if the progressBar 
        /// should be updated. it should be set to false only when working on a query</param>
        private void GetChangeSetInformationByChangeset(
            Changeset changeset,
            bool updateProgressBar = true)
        {
            int changesetId = changeset.ChangesetId;
            AnalyzeChangesOfChangeset(changesetId, changeset.Changes, updateProgressBar);
        }

        /// <summary>
        /// Retrieves all changesets of a workitem by accessing only the linked Items
        /// where the type = changeset.
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
                try
                {
                    ArtifactId artifact = LinkingUtilities.DecodeUri(link.LinkedArtifactUri);
                    if (String.Equals(artifact.ArtifactType, s_Changeset, StringComparison.Ordinal))
                    {
                        // Convert the artifact URI to Changeset object.
                        Changeset changeset = m_ArtifactProvider.GetChangeset
                            (new Uri(link.LinkedArtifactUri));
                        result.Add(changeset);
                    }
                }
                catch (VersionControlException exception)
                {
                    m_ErrorLogWriter.WriteLine(exception);
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

                //get the fileContents of the old/new version of the serverItems
                string oldFileContent;
                string newFileContent;
                GetServerItemsContents
                    (itemId, changesetId, change.ChangeType, out oldFileContent, out newFileContent);

                if (oldFileContent != null &&
                    newFileContent != null)
                {
                    //if a serverItem with the itemId already exists then the methodcomparison result
                    //is aggregated, otherwise a new serverItem is created
                    string serverItemPath = change.Item.ServerItem;
                    ServerItemInformation serverItem = GetOrCreateServerItemInformation
                        (itemId, serverItemPath);

                    //start with the method comparison
                    using (MemoryStream errorStream = new MemoryStream())
                    {
                        MethodComparisonResult methodComparison = MethodComparer.CompareSyntaxTrees
                            (oldFileContent, serverItem.FileName, newFileContent,
                                serverItem.FileName, errorStream);

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
                }

                if (updateProgressBar)
                {
                    UpdateProgressBar();
                }
            }
        }

        /// <summary>
        /// retrieves the contents of the current and the old version of a file.
        /// </summary>
        /// <param name="itemId">specifies the id of the serverItem</param>
        /// <param name="changesetId">specifies the id of the changeset</param>
        /// <param name="changeType">contains the type of change for a file</param>
        /// <param name="oldContent">the content of the previous version of the file</param>
        /// <param name="newContent">the content of the current version of the file</param>
        private void GetServerItemsContents(
            int itemId,
            int changesetId,
            ChangeType changeType,
            out string oldContent,
            out string newContent)
        {
            oldContent = null;
            newContent = null;

            Item currentItem = GetServerItem(itemId, changesetId);

            if (currentItem != null)
            {
                newContent = GetFileString(currentItem);

                if (changeType.HasFlag(ChangeType.Delete))
                {
                    //the file was deleted therefore the old content is set to an empty string
                    //the astcomparer will then mark all methods as deleted
                    oldContent = String.Empty;
                    return;
                }

                //with id - 1 the previous version of a file is returned
                Item previousItem = GetServerItem(itemId, changesetId - 1);

                //If no previous item is found, it is assumed that the currentItem was added in 
                //this changeset. The content of the previous item will be declared with an empty
                //string. The AstComparer will then mark all methods as added.
                oldContent = String.Empty;

                if (previousItem != null)
                {
                    oldContent = GetFileString(previousItem);
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
        /// <param name="message">contains a message which should be printed to the console</param>
        private void InitProgressBar(
            int maxVal,
            string message)
        {
            if (m_ShowProgressBar && m_ConsoleProgressBar != null)
            {
                m_ConsoleProgressBar.SetMaxVal(maxVal);
                m_ConsoleProgressBar.SetLoadFinished(message);
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
