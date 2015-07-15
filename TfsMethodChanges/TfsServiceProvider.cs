using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Tfs_Error_Location;

namespace TfsMethodChanges
{
    public class TfsServiceProvider
    {
        private const string s_TeamProjectCollectionUri =
            "https://iiaastfs.ww004.siemens.net/tfs/TIA";

        private const string s_CodeFileSuffix = ".cs";

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

        public Dictionary<int, ServerItemInformation> GetChangeSetInformationById(int changesetId)
        {
            // Get the changeset for a given Changeset Number
            Changeset changeset = m_VcServer.GetChangeset(changesetId);
            ProcessChangesOfChangeset(changesetId, changeset.Changes);
            return m_ServerItems;
        }

        public Dictionary<int, ServerItemInformation> GetWorkItemInformations(int workItemId)
        {
            WorkItemStore workItemStore = m_TfsTiaProject.GetService<WorkItemStore>();
            WorkItem workItem = workItemStore.GetWorkItem(workItemId);

            try
            {
                IEnumerable<Changeset> linkedChangesets = workItem.Links.OfType<ExternalLink>()
                    .Select(link => m_ArtifactProvider.GetChangeset(new Uri(link.LinkedArtifactUri)));

                foreach (Changeset changeset in linkedChangesets)
                {
                    GetChangeSetInformationByChangeset(changeset);
                }

                return m_ServerItems;
            }
            catch (UriFormatException exception)
            {
                m_ErrorLogWriter.WriteLine(exception.Message);               
            }

            return null;
        }

        public Dictionary<int, ServerItemInformation> GetResultForChangesets(
            IEnumerable<int> changeSets)
        {
            foreach (int changeSet in changeSets)
            {
                GetChangeSetInformationById(changeSet);
            }

            return m_ServerItems;
        }

        public void PrintErrorReport()
        {
            using (StreamReader reader = new StreamReader(m_ErrorLogStream))
            {
                m_ErrorLogStream.Position = 0;
                string errors = reader.ReadToEnd();
                Console.WriteLine(errors);
            }
        }

        private void GetChangeSetInformationByChangeset(Changeset changeset)
        {
            int changesetId = changeset.ChangesetId;
            ProcessChangesOfChangeset(changesetId, changeset.Changes);
        }

        private void ProcessChangesOfChangeset(
            int changesetId,
            IEnumerable<Change> changes)
        {
            foreach (Change change in changes)
            {
                string serverItemPath = change.Item.ServerItem;

                //only work with c# code files
                if (!serverItemPath.EndsWith(s_CodeFileSuffix))
                    continue;

                int itemId = change.Item.ItemId;

                Item currentItem = GetServerItem(itemId, changesetId);

                if (currentItem == null)
                {
                    m_ErrorLogWriter.WriteLine("Error: File: {0} was not found.", itemId);
                    continue;
                }

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

                ServerItemInformation serverItem = GetOrCreateServerItemInformation
                    (itemId, serverItemPath);

                MethodComparisonResult methodComparison = AstComparer.CompareSyntaxTrees
                    (oldFileContent, serverItem.FileName, newFileContent, serverItem.FileName,
                        m_ErrorLogStream);

                if (methodComparison == null)
                    return;

                serverItem.AddChangesetResult(changesetId, methodComparison);
            }
        }

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

        private Item GetServerItem(
            int itemId,
            int changesetId)
        {
            return m_VcServer.GetItem(itemId, changesetId, GetItemsOptions.Download);
        }

        private string GetFileString(Item item)
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
