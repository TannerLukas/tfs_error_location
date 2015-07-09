using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Tfs_Error_Location;

namespace TfsMethodChanges
{
    public class ConsoleSpiner
    {
        // Volatile is used as hint to the compiler that this data 
        // member will be accessed by multiple threads. 
        private volatile bool m_ShouldStop;
        int m_Counter;
        public ConsoleSpiner()
        {
            m_Counter = 0;
            m_ShouldStop = false;
        }

        public void Turn()
        {
            Console.Write("Working....");
            while (!m_ShouldStop)
            {
                //|/-\
                //"/-\\|
                switch (m_Counter % 4)
                {
                    case 0: 
                        Console.Write("|");
                        m_Counter = 0;
                        break;
                    case 1: 
                        Console.Write("/"); 
                        break;
                    case 2: Console.Write("-"); 
                        break;
                    case 3: Console.Write("\\");
                        break;   
                }
                m_Counter++;
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Thread.Sleep(120);
            }
            Console.Write("\r--------FINISHED--------");
        }

        public void RequestStop()
        {
            m_ShouldStop = true;
        }

    }
    
    internal class Program
    {
        private const string s_TeamProjectCollectionUri =
            "https://iiaastfs.ww004.siemens.net/tfs/TIA";

        private const string s_CodeFileSuffix = ".cs";

        private static Dictionary<int, ServerItemInformation> m_ServerItems;

        private static VersionControlServer m_VcServer;
        private static TfsTeamProjectCollection m_TfsTiaProject;
        private static VersionControlArtifactProvider m_ArtifactProvider;
        private static MemoryStream m_ErrorLogStream;



        private static void Main(string[] args)
        {
            ConsoleSpiner spin = new ConsoleSpiner();
            Thread workerThread = new Thread(spin.Turn);
            workerThread.Start();

            using (MemoryStream errorLogStream = new MemoryStream())
            {
                TfsServiceProvider tfsService = new TfsServiceProvider(errorLogStream);
                List<int> changeSets = new List<int> { 1417231, 1470300 };

                string singleResultName = @"c:\temp\SingleComparison.txt";
                string multipleResultName = @"c:\temp\MutlipleComparison.txt";

                int selector = 2;
                Dictionary<int, ServerItemInformation> result;

                switch (selector)
                {
                    case 0:
                        int changesetNumber = 1470300;
                        //int changesetNumber = 63403;
                        result = tfsService.GetChangesetComparisonResult(changesetNumber);
                        //CreateFileReport(result, singleResultName);
                        CreateConsoleReport(result);
                        break;
                    case 1:
                        result = tfsService.GetResultForChangesets(changeSets);
                        CreateFileReport(result, multipleResultName);
                        break;
                    case 2:
                        //int workItemId = 1290403;
                        int workItemId = 986699;
                        result = tfsService.GetWorkItemInformations(workItemId);
                        CreateFileReport(result, multipleResultName);
                        break;
                }

                spin.RequestStop();

                // Use the Join method to block the current thread  
                // until the object's thread terminates.
                workerThread.Join();
            }           
        }

        private static void CreateFileReport(
            Dictionary<int, ServerItemInformation> result,
            string fileName)
        {
            StreamWriter writer = new StreamWriter(fileName, true) {AutoFlush = true};

            foreach (KeyValuePair<int, ServerItemInformation> serverItemInformation in result)
            {
                ServerItemInformation information = serverItemInformation.Value;
                writer.WriteLine
                    ("SERVERITEM: " + information.FileName + " --------------------------");
                information.AggregatedResult.PrintResultOverviewToFile(writer);
                writer.WriteLine();
            }
        }

        private static void CreateConsoleReport(
            Dictionary<int, ServerItemInformation> result)
        {
            foreach (KeyValuePair<int, ServerItemInformation> serverItemInformation in result)
            {
                ServerItemInformation information = serverItemInformation.Value;
                Console.WriteLine
                    ("SERVERITEM: " + information.FileName + " --------------------------");
                information.AggregatedResult.PrintCompleteResultToConsole();
                Console.WriteLine();
            }
        }
    }
}