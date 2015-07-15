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
using NDesk.Options;
using Tfs_Error_Location;

namespace TfsMethodChanges
{
    class Program
    {
        private enum RequestType
        {
            Changeset,
            WorkItem,
            Query
        }

        private enum ExitCode
        {
            Ok = 0,
            NotOk = -1
        }

        private const int s_ReportTableWidth = 100;
        private const string s_ColumnSeperator = "|";

        private const string s_ChangedColumn =
            s_ColumnSeperator + " ChangedCounter " + s_ColumnSeperator;

        private static string s_ReportSeperator;

        private const string s_DashedLine =
            "---------------------------------------------";

        private static OptionSet s_OptionSet;
        private static RequestType s_RequestType;
        private static string s_RequestValue;
        private static bool s_ShowLongReport;
        private static bool s_NoConsoleReport;
        private static string s_ReportFile;
        private static StreamWriter s_FileReportWriter;

        private static ConsoleSpinner s_Spinner;
        private static Thread s_SpinnerThread;

        private const bool s_ShowConsoleSpinner = true;

        private static void Main(string[] args)
        {
            ParseOptions(args);

            try
            {
                if (s_ShowConsoleSpinner)
                {
                    StartSpinningThread();
                }

                Dictionary<int, ServerItemInformation> result = ExecuteRequest
                    (s_RequestType, s_RequestValue);

                CreateReport(result);

                if (s_ShowConsoleSpinner)
                {
                    StopSpinningThread();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                Exit(ExitCode.NotOk);
            }

            Exit(ExitCode.Ok);
        }

        private static void StartSpinningThread()
        {
            s_Spinner = new ConsoleSpinner();
            s_SpinnerThread = new Thread(s_Spinner.Turn);
            s_SpinnerThread.Start();
        }

        private static void StopSpinningThread()
        {
            s_Spinner.RequestStop();

            // Use the Join method to block the current thread  
            // until the object's thread terminates.
            s_SpinnerThread.Join();
        }

        private static Dictionary<int, ServerItemInformation> ExecuteRequest(
            RequestType requestType,
            string requestValue)
        {
            Dictionary<int, ServerItemInformation> result = null;

            try
            {
                TfsServiceProvider tfsService = new TfsServiceProvider();

                switch (requestType)
                {
                    case RequestType.Changeset:
                        int changesetId = Convert.ToInt32(requestValue);
                        result = tfsService.GetChangeSetInformationById(changesetId);
                        break;
                    case RequestType.WorkItem:
                        int workItemId = Convert.ToInt32(requestValue);
                        result = tfsService.GetWorkItemInformations(workItemId);
                        break;
                    case RequestType.Query:
                        //no need for conversion
                        //TODO: process a query
                        break;
                }

                tfsService.PrintErrorReport();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                Exit(ExitCode.NotOk);
            }

            return result;
        }

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
                    "outputfile=|file=", "Stores the report in {FILE}. Default directory= /bin/debug/",
                    v => s_ReportFile = v
                },
                {
                    "noconsole|noc", "Show no report on the console. Default=False",
                    v => s_NoConsoleReport = v != null
                },
                {
                    "long|l", "Prints a long version of the report. Default=short version.",
                    v => s_ShowLongReport = v != null
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

            if (s_ReportFile != null)
            {
                InitFileReportWriter(s_ReportFile);
            }

            //if this point is reached, the parsing of the commandline options
            //was successful. Therefore, requestvalue consists of exactly one value. 
            s_RequestType = requestValues.First().Key;
            s_RequestValue = requestValues.First().Value;
        }

        private static void HandleException(Exception exception)
        {
            Console.WriteLine(exception.Message);
            Exit(ExitCode.NotOk);
        }

        private static void InitFileReportWriter(string fileName)
        {
            try
            {
                s_FileReportWriter = new StreamWriter(fileName, false) {AutoFlush = true};
            }
            catch (IOException e)
            {
                HandleException(e);
            }
            catch (ArgumentException e)
            {
                HandleException(e);
            }
            catch (UnauthorizedAccessException e)
            {
                HandleException(e);
            }
        }

        private static void ShowHelp(ExitCode exitCode)
        {
            Console.WriteLine("Command line options:");
            s_OptionSet.WriteOptionDescriptions(Console.Out);

            Exit(exitCode);
        }

        private static void Exit(ExitCode exitCode)
        {
            Environment.Exit((int)exitCode);
        }

        private static void CreateReport(Dictionary<int, ServerItemInformation> result)
        {
            if (!s_NoConsoleReport)
            {
                CreateConsoleReport(result);
            }

            if (s_FileReportWriter != null)
            {
                CreateFileReport(result);
            }
        }

        private static void CreateFileReport(Dictionary<int, ServerItemInformation> result)
        {
            //create the needed headers/seperators
            s_ReportSeperator = CreateFileReportSeperator(s_ReportTableWidth);
            string methodHeader = CreateFileReportMethodHeader();

            foreach (KeyValuePair<int, ServerItemInformation> serverItemInformation in result)
            {
                //print serveritem row
                s_FileReportWriter.WriteLine(s_ReportSeperator);
                ServerItemInformation information = serverItemInformation.Value;
                string text = s_ColumnSeperator + " \t SERVERITEM: " + information.FileName;
                s_FileReportWriter.Write(text.PadRight(s_ReportTableWidth));
                s_FileReportWriter.WriteLine(s_ColumnSeperator);
                s_FileReportWriter.WriteLine(s_ReportSeperator);

                //print methodheader row
                s_FileReportWriter.WriteLine(methodHeader);
                s_FileReportWriter.WriteLine(s_ReportSeperator);

                //print the the methods with their counters
                information.AggregatedResult.PrintResultOverviewToFile
                    (s_FileReportWriter, s_ReportTableWidth, s_ColumnSeperator, true);

                s_FileReportWriter.WriteLine(s_ReportSeperator);
                s_FileReportWriter.WriteLine();
            }
        }

        private static void CreateConsoleReport(Dictionary<int, ServerItemInformation> result)
        {
            foreach (KeyValuePair<int, ServerItemInformation> serverItemInformation in result)
            {
                Console.WriteLine(s_DashedLine);
                ServerItemInformation information = serverItemInformation.Value;
                Console.WriteLine("| SERVERITEM: " + information.FileName);
                information.AggregatedResult.PrintCompleteResultToConsole();
                Console.WriteLine();
            }
        }

        private static string CreateFileReportMethodHeader()
        {
            string methodHeader = s_ColumnSeperator +
                                  "\tMETHOD".PadRight(s_ReportTableWidth - s_ChangedColumn.Length - 1) +
                                  s_ChangedColumn;
            return methodHeader;
        }

        private static string CreateFileReportSeperator(int length)
        {
            return (" " + new string('-', length));
        }
    }
}