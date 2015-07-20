using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NDesk.Options;
using Tfs_Error_Location;

namespace TfsMethodChanges
{
    internal class Program
    {
        /// <summary>
        /// describes the different types of requests which could be send to 
        /// the TfsServiceProvider.
        /// </summary>
        private enum RequestType
        {
            Changeset,
            WorkItem,
            Query
        }

        /// <summary>
        /// contains the possible exit codes of the program
        /// </summary>
        private enum ExitCode
        {
            Ok = 0,
            NotOk = -1
        }

        private const string s_DashedString = "--------";

        private const string s_FinishedOk = s_DashedString + " FINISHED " + s_DashedString + "\r\n";

        private const string s_FinishedError =
            s_DashedString + " Stopped working due to errors: " + s_DashedString + "\r\n";

        private const string s_StandardReportFile = "report.txt";

        private const int s_ReportTableWidth = 100;
        private const string s_ColumnSeperator = "|";

        private const string s_ChangedColumn =
            s_ColumnSeperator + " ChangedCounter " + s_ColumnSeperator;

        private static OptionSet s_OptionSet;
        private static RequestType s_RequestType;
        private static string s_RequestValue;
        private static string s_ReportSeperator;
        private static string s_ReportFile;

        private static ConsoleSpinner s_Spinner;
        private static Thread s_SpinnerThread;

        private const bool s_ShowConsoleSpinner = true;

        private static void Main(string[] args)
        {
            ParseOptions(args);

            try
            {
                using (
                    StreamWriter reportWriter = new StreamWriter(s_ReportFile, false)
                    {
                        AutoFlush = true
                    })
                {
                    Dictionary<int, ServerItemInformation> result = ExecuteRequest
                        (s_RequestType, s_RequestValue);

                    CreateFileReport(reportWriter, result);
                }
            }
            catch (Exception exception)
            {
                StopSpinningThread(s_FinishedError);
                HandleException(exception);
            }

            StopSpinningThread(s_FinishedOk);
            Exit(ExitCode.Ok);
        }

        private static void StartSpinningThread()
        {
            if (s_ShowConsoleSpinner)
            {
                s_Spinner = new ConsoleSpinner();
                s_SpinnerThread = new Thread(s_Spinner.Turn);
                s_SpinnerThread.Start();
            }
        }

        private static void StopSpinningThread(string message)
        {
            if (s_SpinnerThread != null &&
                s_SpinnerThread.IsAlive)
            {
                s_Spinner.RequestStop();

                // Use the Join method to block the current thread  
                // until the object's thread terminates.
                s_SpinnerThread.Join();

                Console.Write("\r" + message + "\r\n");
            }
        }

        /// <summary>
        /// executes a request based on the requestType and value
        /// </summary>
        /// <param name="requestType">specifies which type of request should be executed <see cref="RequestType"/></param>
        /// <param name="requestValue">a string which contains the changesetID/workItemID/queryID</param>
        /// <returns>a Dictionary which contains all ServerItems</returns>
        private static Dictionary<int, ServerItemInformation> ExecuteRequest(
            RequestType requestType,
            string requestValue)
        {
            Dictionary<int, ServerItemInformation> result = null;

            TfsServiceProvider tfsService = new TfsServiceProvider();

            switch (requestType)
            {
                case RequestType.Changeset:
                    int changesetId = Convert.ToInt32(requestValue);
                    Console.WriteLine("Started working on changeset: " + changesetId + "\r\n");
                    StartSpinningThread();
                    result = tfsService.GetChangeSetInformationById(changesetId);
                    break;
                case RequestType.WorkItem:
                    int workItemId = Convert.ToInt32(requestValue);
                    Console.WriteLine("Started working on workItem: " + workItemId + "\r\n");
                    StartSpinningThread();
                    result = tfsService.GetWorkItemInformations(workItemId);
                    break;
                case RequestType.Query:
                    //no need for conversion
                    //TODO: process a query
                    break;
            }

            //check if the result was created successfully
            if (result == null)
            {
                StopSpinningThread(s_FinishedError);
                //print the tfs related errors
                tfsService.PrintErrorReport();
                Exit(ExitCode.NotOk);
            }

            return result;
        }

        /// <summary>
        /// parses all options from the command line 
        /// </summary>
        /// <param name="args">the arguments from the command line</param>
        private static void ParseOptions(IEnumerable<string> args)
        {
            string changesetId = null;
            string workItemId = null;
            string queryId = null;

            s_OptionSet = new OptionSet
            {
                {"h|?|help", "Show this help message.", v => ShowHelp(0)},
                {
                    "changeset|c=", "RequestOption with {CHANGESETID} for MethodComparison.",
                    v => changesetId = v
                },
                {
                    "workitem|w=", "RequestOption with  {WORKITEMID} for MethodComparison.",
                    v => workItemId = v
                },
                {
                    "query|q=", "RequestOption with  {QUERYID} for MethodComparison.", v => queryId = v
                },
                {
                    "outputfile=|file=", "Stores the report in {FILE}. Default = /bin/debug/report.txt",
                    v => s_ReportFile = v
                }
            };

            try
            {
                List<string> extras = s_OptionSet.Parse(args);

                if (extras.Any())
                {
                    Console.WriteLine("Unknown parameters.");
                    ShowHelp(ExitCode.NotOk);
                }
            }
            catch (OptionException e)
            {
                HandleException(e);
            }

            Dictionary<RequestType, string> request = new Dictionary<RequestType, string>
            {
                {RequestType.Changeset, changesetId},
                {RequestType.WorkItem, workItemId},
                {RequestType.Query, queryId}
            };

            //get all requests where a corresponding request value was set
            var requestValues = request.Where(r => r.Value != null).ToList();

            if (requestValues.Count() > 1 ||
                !requestValues.Any())
            {
                //wrong amount of command line parameters.
                Console.WriteLine("A single request Option should be used.");
                ShowHelp(ExitCode.NotOk);
            }

            //use the standard report file name if no file name was given
            if (s_ReportFile == null)
            {
                s_ReportFile = s_StandardReportFile;
            }

            //if this point is reached, the parsing of the commandline options
            //was successful. Therefore, requestValues consists of exactly one value. 
            s_RequestType = requestValues.First().Key;
            s_RequestValue = requestValues.First().Value;
        }

        private static void HandleException(Exception exception)
        {
            Console.WriteLine(exception.Message);
            Exit(ExitCode.NotOk);
        }

        /// <summary>
        /// writes the help message of the option set to the console
        /// </summary>
        /// <param name="exitCode"></param>
        private static void ShowHelp(ExitCode exitCode)
        {
            Console.WriteLine("Command line options:");
            s_OptionSet.WriteOptionDescriptions(Console.Out);

            Exit(exitCode);
        }

        /// <summary>
        /// exits the program with the specified exitCode
        /// </summary>
        /// <param name="exitCode">the code which is returned when the program exits</param>
        private static void Exit(ExitCode exitCode)
        {
            Environment.Exit((int)exitCode);
        }

        /// <summary>
        /// creates the report file for the correct and error items
        /// </summary>
        /// <param name="writer">StreamWriter used to write to the report file</param>
        /// <param name="result">contains all serverItems with their
        /// aggregated method comparison result</param>
        private static void CreateFileReport(
            StreamWriter writer,
            Dictionary<int, ServerItemInformation> result)
        {
            //create the needed headers/seperators
            s_ReportSeperator = CreateFileReportSeperator(s_ReportTableWidth);

            IEnumerable<ServerItemInformation> serverItems = result.Values;

            IEnumerable<ServerItemInformation> correctItems = serverItems.Where
                (s => (s.AggregatedResult != null && !s.Errors.Any()));

            IEnumerable<ServerItemInformation> errorItems = serverItems.Except(correctItems);

            PrintErrorItemsToFile(writer, errorItems);
            PrintCorrectItemsToFile(writer, correctItems);
        }

        /// <summary>
        /// prints all correct items in a formatted table into a file.
        /// </summary>
        /// <param name="writer">StreamWriter used to write to the report file</param>
        /// <param name="items">the items which were successfully processed</param>
        private static void PrintCorrectItemsToFile(
            StreamWriter writer,
            IEnumerable<ServerItemInformation> items)
        {
            string methodHeader = CreateFileReportMethodHeader();

            foreach (ServerItemInformation item in items)
            {
                //print serveritem row
                writer.WriteLine(s_ReportSeperator);

                string itemName = item.FileName;

                if (item.IsDeleted)
                {
                    itemName = itemName + " (DELETED)";
                }

                string text = s_ColumnSeperator + " \t SERVERITEM: " + itemName;
                writer.Write(text.PadRight(s_ReportTableWidth));
                writer.WriteLine(s_ColumnSeperator);
                writer.WriteLine(s_ReportSeperator);

                //print methodheader row
                writer.WriteLine(methodHeader);
                writer.WriteLine(s_ReportSeperator);

                //print the the methods with their counters
                item.AggregatedResult.PrintResultOverviewToFile
                    (writer, s_ReportTableWidth, s_ColumnSeperator, false);

                writer.WriteLine(s_ReportSeperator);
                writer.WriteLine();
            }
        }

        /// <summary>
        /// prints all items in which the comparison procedure lead to an error to a file.
        /// </summary>
        /// <param name="writer">StreamWriter used to write to the report file</param>
        /// <param name="items">the items which were not successfully processed</param>
        private static void PrintErrorItemsToFile(
            StreamWriter writer,
            IEnumerable<ServerItemInformation> items)
        {
            if (items.Any())
            {
                writer.WriteLine("\r\nERRORS in ServerItems: ");

                foreach (ServerItemInformation item in items)
                {
                    writer.WriteLine("\t # ServerItem: " + item.FileName);
                }

                writer.WriteLine();
                writer.WriteLine();
            }
        }

        /// <summary>
        /// creates a the method header string for the file report. padding is
        /// used to align to table and its rows/columns correctly.
        /// </summary>
        /// <returns>the method header string for the file report</returns>
        private static string CreateFileReportMethodHeader()
        {
            string methodHeader = s_ColumnSeperator +
                                  "\tMETHOD".PadRight
                                      (s_ReportTableWidth - s_ChangedColumn.Length - 1) +
                                  s_ChangedColumn;
            return methodHeader;
        }

        /// <summary>
        /// creates the file report seperator for a given length
        /// </summary>
        /// <param name="length">indicates how long the seperator should be</param>
        /// <returns>the string containing the file report seperator.</returns>
        private static string CreateFileReportSeperator(int length)
        {
            return (" " + new string('-', length));
        }
    }
}