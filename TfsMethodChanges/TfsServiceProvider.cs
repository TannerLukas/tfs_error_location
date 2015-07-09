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

        public TfsServiceProvider(MemoryStream errorLogStream)
        {
            m_ServerItems = new Dictionary<int, ServerItemInformation>();
            m_TfsTiaProject = TfsTeamProjectCollectionFactory.GetTeamProjectCollection
                (new Uri(s_TeamProjectCollectionUri));
            m_VcServer = m_TfsTiaProject.GetService<VersionControlServer>();
            m_ArtifactProvider = m_VcServer.ArtifactProvider;
            m_ErrorLogStream = errorLogStream;
        }

        public Dictionary<int, ServerItemInformation> GetWorkItemInformations(int workItemId)
        {
            WorkItemStore workItemStore = m_TfsTiaProject.GetService<WorkItemStore>();
            WorkItem workItem = workItemStore.GetWorkItem(workItemId);

            IEnumerable<Changeset> linkedChangesets = workItem.Links.OfType<ExternalLink>()
                .Select(link => m_ArtifactProvider.GetChangeset(new Uri(link.LinkedArtifactUri)));

            foreach (Changeset changeset in linkedChangesets)
            {
                GetChangeSetInformationByChangeset(changeset);
            }

            return m_ServerItems;
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

        public Dictionary<int, ServerItemInformation> GetChangesetComparisonResult(
            int changesetNumber)
        {
            GetChangeSetInformationById(changesetNumber);
            return m_ServerItems;
        }

        private void GetChangeSetInformationByChangeset(Changeset changeset)
        {
            int changesetId = changeset.ChangesetId;
            ProcessChangesOfChangeset(changesetId, changeset.Changes);
        }

        private void GetChangeSetInformationById(int changesetId)
        {
            // Get the changeset for a given Changeset Number
            Changeset changeset = m_VcServer.GetChangeset(changesetId);
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

                Item currentItem = m_VcServer.GetItem(itemId, changesetId, GetItemsOptions.Download);

                if (currentItem == null)
                {
                    continue;
                }

                //The  GetItem() Method returns null if no object is found.
                Item previousItem = m_VcServer.GetItem
                    (itemId, changesetId - 1, GetItemsOptions.Download);

                string oldFileContent = String.Empty;

                if (previousItem != null)
                {
                    oldFileContent = GetFileString(previousItem);
                }

                string newFileContent = GetFileString(currentItem);

                ServerItemInformation information;

                if (m_ServerItems.ContainsKey(itemId))
                {
                    //merge
                    information = m_ServerItems[itemId];

                    MethodComparisonResult methodComparison = AstComparer.CompareSyntaxTrees
                        (oldFileContent, information.FileName, newFileContent, information.FileName,
                            m_ErrorLogStream);

                    information.AddChangesetResult(changesetId, methodComparison);
                }
                else
                {
                    //create new item
                    information = new ServerItemInformation(serverItemPath);

                    MethodComparisonResult methodComparison = AstComparer.CompareSyntaxTrees
                        (oldFileContent, information.FileName, newFileContent, information.FileName,
                            m_ErrorLogStream);

                    information.AddChangesetResult(changesetId, methodComparison);
                    m_ServerItems.Add(itemId, information);
                }
            }
        }

        public string GetFileString(Item item)
        {
            // Setup string container
            string content = string.Empty;

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
