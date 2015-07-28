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
            QueryName,
            QueryPerson,
            QueryString
        }

        /// <summary>
        /// contains the possible exit codes of the program
        /// </summary>
        private enum ExitCode
        {
            Ok = 0,
            NotOk = -1
        }

        private const string s_StandardReportFile = "report.txt";
        private const string s_DefaultIniFile = "config.ini";

        private const int s_ReportTableWidth = 100;
        private const string s_ColumnSeperator = "|";

        private const string s_ChangedColumn =
            s_ColumnSeperator + " ChangedCounter " + s_ColumnSeperator;

        private static OptionSet s_OptionSet;
        private static RequestType s_RequestType;
        private static string s_RequestValue;
        private static string s_ReportSeperator;
        private static string s_ReportFile;
        private static string s_IniFile;

        private static ConsoleProgressBar s_ConsoleProgressBar;
        private static Thread s_ProgressThread;
        private const bool s_ShowProgressBar = true;

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
                    //retrieve the tfsConfiguration which should be used from the iniFile
                    TfsConfiguration tfsConfiguration = LoadIniFile(s_IniFile);

                    StartProgressThread();

                    Dictionary<int, ServerItemInformation> result = ExecuteRequest
                        (tfsConfiguration, s_RequestType, s_RequestValue);

                    CreateFileReport(reportWriter, result);
                }
            }
            catch (Exception exception)
            {
                StopProgressThread("An Error occurred: ", true);
                HandleException(exception);
            }

            Exit(ExitCode.Ok);
        }

        /// <summary>
        /// parses all options from the command line 
        /// </summary>
        /// <param name="args">the arguments from the command line</param>
        private static void ParseOptions(IEnumerable<string> args)
        {
            string changesetId = null;
            string workItemId = null;
            string query = null;
            string queryPerson = null;
            string queryString = null;

            s_OptionSet = new OptionSet
            {
                {"h|?|help", "Show this help message.", v => ShowHelp(0)},
                {
                    "changeset|c=", "RequestOption with {CHANGESETID} for MethodComparison.",
                    v => changesetId = v
                },
                {
                    "workitem|w=", "RequestOption with {WORKITEMID} for MethodComparison.",
                    v => workItemId = v
                },
                {
                    "query|q=",
                    @"RequestOption with {QUERYPATH/QUERYNAME} for MethodComparison. Has to be between """".",
                    v => query = v
                },
                {
                    "qperson|qp=",
                    @"RequestOption with {QUERYPERSON} for MethodComparison. Has to be between """".",
                    v => queryPerson = v
                },
                {
                    "qstring|qs=",
                    @"RequestOption with {QUERYSTRING} for MethodComparison. Has to be between """".",
                    v => queryString = v
                },
                {
                    "ini=|i=", "Loads the configuration saved in {FILE}. Default =  ./config.ini",
                    v => s_IniFile = v
                },
                {
                    "outputfile=|f=", "Stores the report in {FILE}. Default =  ./report.txt",
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
                {RequestType.QueryName, query},
                {RequestType.QueryPerson, queryPerson},
                {RequestType.QueryString, queryString}
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

            //if this point is reached, only a single command line option was given        
            s_RequestType = requestValues.First().Key;
            s_RequestValue = requestValues.First().Value;

            CheckRequest(s_RequestType, s_RequestValue);

            //use the standard report file if no file name was given
            if (s_ReportFile == null)
            {
                s_ReportFile = s_StandardReportFile;
            }

            //use the standard iniFile if no file name was given
            if (s_IniFile == null)
            {
                s_IniFile = s_DefaultIniFile;
            }
        }

        /// <summary>
        /// checks the correctness of a requestValue
        /// </summary>
        /// <param name="type">specifies the type of a request</param>
        /// <param name="requestValue">specifies the value of a request</param>
        private static void CheckRequest(
            RequestType type,
            string requestValue)
        {
            //add check for the request value
            if (requestValue.Equals(String.Empty))
            {
                //wrong amount of command line parameters.
                Console.WriteLine("Please define a value for the request.");
                ShowHelp(ExitCode.NotOk);
            }

            if (type == RequestType.Changeset ||
                type == RequestType.WorkItem)
            {
                //the request value has to contain a positiv number
                uint parsedValue;
                if (!uint.TryParse(s_RequestValue, out parsedValue))
                {
                    Console.WriteLine("Please define a positive value for the request.");
                    ShowHelp(ExitCode.NotOk);
                }
            }
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
        /// loads the contents of an iniFile
        /// </summary>
        /// <param name="iniFile">the name of the iniFile which should be used</param>
        /// <returns>on success: a TfsConfiguration object, otherwise the program exits</returns>
        private static TfsConfiguration LoadIniFile(string iniFile)
        {
            try
            {
                MemoryStream errorStream = new MemoryStream();

                using (StreamWriter errorWriter = new StreamWriter(errorStream) { AutoFlush = true })
                {
                    using (
                        FileStream fileStream = new FileStream
                            (iniFile, FileMode.Open, FileAccess.Read))
                    {
                        //contains all sections from the IniFile
                        IEnumerable<Section> sections = IniFileParser.ReadIniFile
                            (fileStream, errorWriter);

                        if (sections != null)
                        {
                            TfsConfiguration configuration = IniFileInterpreter.InterpretIniFile
                                (sections, errorWriter);

                            if (configuration != null)
                            {
                                return configuration;
                            }
                        }

                        //an error has occurred
                        PrintErrorsAndExit(errorStream);
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("IniFile Error: " + e.Message);
                Exit(ExitCode.NotOk);
            }

            return null;
        }

        /// <summary>
        /// based on the s_ShowProgressBar flag a ProgressBar Thread is created and started.
        /// </summary>
        private static void StartProgressThread()
        {
            if (s_ShowProgressBar)
            {
                s_ConsoleProgressBar = new ConsoleProgressBar();
                s_ProgressThread = new Thread(s_ConsoleProgressBar.Turn);
                s_ProgressThread.Start();
            }
        }

        /// <summary>
        /// stops the progressBar thread and prints a
        /// </summary>
        /// <param name="message">an message which is printed after the thread has finished</param>
        /// <param name="errorOccured">a flag which indicates if an error occurred</param>
        private static void StopProgressThread(
            string message,
            bool errorOccured)
        {
            if (s_ProgressThread != null &&
                s_ProgressThread.IsAlive)
            {
                if (errorOccured)
                {
                    s_ConsoleProgressBar.SetErrorOccurred();
                }
                s_ConsoleProgressBar.RequestStop();

                // Use the Join method to block the current thread  
                // until the object's thread terminates.
                s_ProgressThread.Join();
                Console.ResetColor();

                if (errorOccured)
                {
                    Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
                }

                Console.Write(message);
            }
        }

        /// <summary>
        /// executes a request based on the requestType and value
        /// </summary>
        /// <param name="config">defines the used configuration of the tfsServer</param>
        /// <param name="requestType">specifies which type of request should be executed <see cref="RequestType"/></param>
        /// <param name="requestValue">a string which contains the changesetID/workItemID/queryID</param>
        /// <returns>a Dictionary which contains all ServerItems</returns>
        private static Dictionary<int, ServerItemInformation> ExecuteRequest(
            TfsConfiguration config,
            RequestType requestType,
            string requestValue)
        {
            Dictionary<int, ServerItemInformation> result = null;

            TfsServiceProvider tfsService = new TfsServiceProvider
                (config, s_ConsoleProgressBar, s_ShowProgressBar);

            switch (requestType)
            {
                case RequestType.Changeset:
                    int changesetId = Convert.ToInt32(requestValue);
                    result = tfsService.ExecuteChangesetRequest(changesetId);
                    break;
                case RequestType.WorkItem:
                    int workItemId = Convert.ToInt32(requestValue);
                    result = tfsService.ExecuteWorkItemRequest(workItemId);
                    break;
                case RequestType.QueryName:
                    result = tfsService.ExecuteQueryByName(requestValue);
                    break;
                case RequestType.QueryPerson:
                    result = tfsService.ExecuteQueryForPerson(requestValue);
                    break;
                case RequestType.QueryString:
                    result = tfsService.ExecuteQueryString(requestValue);
                    break;
            }

            //check if the result was created successfully
            if (result == null)
            {
                StopProgressThread("An Error occurred: ", true);
                //print the tfs related errors
                tfsService.PrintErrorReport();
                Exit(ExitCode.NotOk);
            }

            StopProgressThread(String.Empty, false);

            return result;
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

        /// <summary>
        /// prints the exception message and exits the program
        /// </summary>
        /// <param name="exception">the exception which occurred</param>
        private static void HandleException(Exception exception)
        {
            Console.WriteLine("\r\n" + exception.Message);
            Exit(ExitCode.NotOk);
        }

        /// <summary>
        /// prints all errors of the errorStream and exits the program
        /// </summary>
        /// <param name="errorStream">Stream with all errors</param>
        private static void PrintErrorsAndExit(Stream errorStream)
        {
            errorStream.Position = 0;
            using (StreamReader reader = new StreamReader(errorStream))
            {
                Console.WriteLine(reader.ReadToEnd());
            }

            Exit(ExitCode.NotOk);
        }

        /// <summary>
        /// exits the program with the specified exitCode
        /// </summary>
        /// <param name="exitCode">the code which is returned when the program exits</param>
        private static void Exit(ExitCode exitCode)
        {
            Environment.Exit((int)exitCode);
        }
    }
}