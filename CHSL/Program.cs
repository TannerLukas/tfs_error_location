using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NDesk.Options;

namespace CHSL
{
    internal class Program
    {
        /// <summary>
        /// describes the different types of queries which could be send to 
        /// the TfsServiceProvider
        /// </summary>
        private enum QueryType
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

        private const string s_StandardReportFile = "report.csv";
        private const string s_DefaultIniFile = "config.ini";

        private const char s_Seperator = ';';
        private const string s_CsvExtension = ".csv";
        private const string s_TxtExtension = ".txt";

        //column definitions which are used in the report file
        private const string s_ServerItemColumn = "FileName";
        private const string s_NamespaceColumn = "Namespace";
        private const string s_ClassColumn = "Class";
        private const string s_MethodNameColumn = "MethodName";
        private const string s_ParametersColumn = "Parameters";
        private const string s_CounterColumn = "MethodVolatility";

        private static OptionSet s_OptionSet;
        private static QueryType s_QueryType;
        private static string s_QueryValue;

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
                //retrieve the tfsConfiguration which should be used from the iniFile
                TfsConfiguration tfsConfiguration = LoadIniFile(s_IniFile);

                Dictionary<int, ServerItemInformation> result = ExecuteQuery
                    (tfsConfiguration, s_QueryType, s_QueryValue);

                PrintReport(result);
            }
            catch (Exception exception)
            {
                StopProgressThread(true);
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
            string queryName = null;
            string queryPerson = null;
            string queryString = null;

            s_OptionSet = new OptionSet
            {
                {"h|?|help", "Show this help message.", v => ShowHelp(0)},
                {
                    "changeset|c=", "QueryOption with {CHANGESETID} for MethodComparison.",
                    v => changesetId = v
                },
                {
                    "workitem|w=", "QueryOption with {WORKITEMID} for MethodComparison.",
                    v => workItemId = v
                },
                {
                    "query|q=",
                    @"QueryOption with ""{QUERYPATH/QUERYNAME}"" for MethodComparison.",
                    v => queryName = v
                },
                {
                    "qperson|qp=",
                    @"QueryOption with ""{QUERYPERSON}"" for MethodComparison.",
                    v => queryPerson = v
                },
                {
                    "qstring|qs=",
                    @"QueryOption with ""{QUERYSTRING}"" for MethodComparison.",
                    v => queryString = v
                },
                {
                    "ini=|i=", "Loads the configuration from {FILE}. \nDefault =  ./config.ini",
                    v => s_IniFile = v
                },
                {
                    "outputfile=|o=",
                    "Stores the report in {FILE}. \nDefault =  ./" + s_StandardReportFile,
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

            Dictionary<QueryType, string> query = new Dictionary<QueryType, string>
            {
                {QueryType.Changeset, changesetId},
                {QueryType.WorkItem, workItemId},
                {QueryType.QueryName, queryName},
                {QueryType.QueryPerson, queryPerson},
                {QueryType.QueryString, queryString}
            };

            //get all requests where a corresponding request value was set
            var queryValues = query.Where(r => r.Value != null).ToList();

            if (queryValues.Count() > 1 ||
                !queryValues.Any())
            {
                //wrong amount of command line parameters.
                Console.WriteLine("A single query option should be used.");
                ShowHelp(ExitCode.NotOk);
            }

            //if this point is reached, only a single command line option was given        
            s_QueryType = queryValues.First().Key;
            s_QueryValue = queryValues.First().Value;

            CheckForValidQuery(s_QueryType, s_QueryValue);

            CheckForValidReportFile();

            //use the standard iniFile if no file name was given
            if (s_IniFile == null)
            {
                s_IniFile = s_DefaultIniFile;
            }
        }

        /// <summary>
        /// checks if the given reportFileName is valid:
        /// .) if it is valid the reportFile is created.
        /// .) otherwise an exception is thrown and the program exits.
        /// </summary>
        private static void CheckForValidReportFile()
        {
            if (s_ReportFile == null)
            {
                //use the standard report file if no file name was given
                s_ReportFile = s_StandardReportFile;
            }
            else if (!s_ReportFile.EndsWith(s_TxtExtension) &&
                     !s_ReportFile.EndsWith(s_CsvExtension))
            {
                Console.WriteLine
                    ("Please define a valid ReportFile extension with either " + s_CsvExtension +
                     " or " + s_TxtExtension + ".");
                ShowHelp(ExitCode.NotOk);
            }

            //try to create it
            try
            {
                if (File.Exists(s_ReportFile))
                {
                    File.Delete(s_ReportFile);
                }

                File.Create(s_ReportFile).Close();
            }
            catch (IOException exception)
            {
                HandleException(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                HandleException(exception);
            }
            catch (ArgumentException exception)
            {
                HandleException(exception);
            }
        }

        /// <summary>
        /// checks the correctness of a queryValue
        /// </summary>
        /// <param name="type">specifies the type of a request</param>
        /// <param name="queryValue">specifies the value of a request</param>
        private static void CheckForValidQuery(
            QueryType type,
            string queryValue)
        {
            //add check for the request value
            if (queryValue.Equals(String.Empty))
            {
                //wrong amount of command line parameters.
                Console.WriteLine("Please define a value for the query.");
                ShowHelp(ExitCode.NotOk);
            }

            if (type == QueryType.Changeset ||
                type == QueryType.WorkItem)
            {
                //the queryValue has to contain a positiv number
                uint parsedValue;
                if (!uint.TryParse(s_QueryValue, out parsedValue))
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

                using (StreamWriter errorWriter = new StreamWriter(errorStream) {AutoFlush = true})
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
            }
        }

        /// <summary>
        /// executes a request based on the requestType and value
        /// </summary>
        /// <param name="config">defines the used configuration of the tfsServer</param>
        /// <param name="queryType">specifies which type of request should be executed <see cref="QueryType"/></param>
        /// <param name="requestValue">a string which contains the changesetID/workItemID/queryID</param>
        /// <returns>a Dictionary which contains all ServerItems</returns>
        private static Dictionary<int, ServerItemInformation> ExecuteQuery(
            TfsConfiguration config,
            QueryType queryType,
            string requestValue)
        {
            StartProgressThread();

            Dictionary<int, ServerItemInformation> result = null;

            TfsServiceProvider tfsService = new TfsServiceProvider
                (config, s_ConsoleProgressBar, s_ShowProgressBar);

            switch (queryType)
            {
                case QueryType.Changeset:
                    int changesetId = Convert.ToInt32(requestValue);
                    result = tfsService.ExecuteChangesetRequest(changesetId);
                    break;
                case QueryType.WorkItem:
                    int workItemId = Convert.ToInt32(requestValue);
                    result = tfsService.ExecuteWorkItemRequest(workItemId);
                    break;
                case QueryType.QueryName:
                    result = tfsService.ExecuteQueryByName(requestValue);
                    break;
                case QueryType.QueryPerson:
                    result = tfsService.ExecuteQueryForPerson(requestValue);
                    break;
                case QueryType.QueryString:
                    result = tfsService.ExecuteQueryString(requestValue);
                    break;
            }

            //check if the result was created successfully
            if (result == null)
            {
                StopProgressThread(true);
                //print the tfs related errors
                tfsService.PrintErrorReport();
                Exit(ExitCode.NotOk);
            }

            StopProgressThread(false);
            tfsService.PrintErrorReport();

            return result;
        }

        /// <summary>
        /// creates the final report of the comparison procedure.
        /// the files which were compared successfully are printed to a file,
        /// the files where the comparison lead to errors are printed to the console
        /// </summary>
        /// <param name="result"></param>
        private static void PrintReport(Dictionary<int, ServerItemInformation> result)
        {
            using (StreamWriter writer = new StreamWriter(s_ReportFile, false))
            {
                //retrieve all serverItems
                IEnumerable<ServerItemInformation> serverItems = result.Values;

                //retrieve all serverItems which were processed successfully
                IEnumerable<ServerItemInformation> correctItems = serverItems.Where
                    (s => (s.AggregatedResult != null && !s.Errors.Any()));

                //retrieve and print all serverItems which were processed with errors
                IEnumerable<ServerItemInformation> errorItems = serverItems.Except(correctItems);
                PrintErrorItems(errorItems);

                PrintCorrectItems(correctItems, writer);
            }
        }

        /// <summary>
        /// prints all successfully processed ServerItems to the reportFile
        /// </summary>
        /// <param name="items">contains all successfully processed ServerItems</param>
        /// <param name="writer">the StreamWriter which writes to the file</param>
        /// <param name="sortItems">an optional flag which could be set in order to sort the rows</param>
        private static void PrintCorrectItems(
            IEnumerable<ServerItemInformation> items,
            StreamWriter writer,
            bool sortItems = true)
        {
            //define the columns which should be printed
            List<string> columns = new List<string>
            {
                s_ServerItemColumn,
                s_NamespaceColumn,
                s_ClassColumn,
                s_MethodNameColumn,
                s_ParametersColumn,
                s_CounterColumn
            };

            //has to be casted to a string
            string seperator = s_Seperator + String.Empty;
            string headline = String.Join(seperator, columns);

            //get all rowEntries which should be printed
            List<string> rows = new List<string>();
            foreach (ServerItemInformation item in items)
            {
                string itemName = item.FileName;
                List<string> methods = item.AggregatedResult.CreateReportMethodRows(s_Seperator);

                //create for each method a corresponding rowEntry
                rows.AddRange(methods.Select(method => itemName + s_Seperator + method));
            }

            //print the column names to the file
            writer.WriteLine(headline);

            if (sortItems)
            {
                int counterIndex = columns.IndexOf(s_CounterColumn);
                int namespaceIndex = columns.IndexOf(s_NamespaceColumn);

                if (counterIndex != -1 &&
                    namespaceIndex != -1)
                {
                    //sort them before they are printed
                    IEnumerable<string> sortedRows = from row in rows
                        let cells = row.Split(s_Seperator)
                        orderby cells[counterIndex] descending, 
                        cells[namespaceIndex] ascending
                        select row;

                    //print the sorted rows to the reportFile
                    PrintRowsToFile(writer, sortedRows);
                    return;
                }
            }

            //the unsorted rows should be printed if either no sorting was requested
            //or the sorting mechanism could not be applied
            PrintRowsToFile(writer, rows);
        }

        /// <summary>
        /// prints all rows to the underlying stream
        /// </summary>
        /// <param name="writer">the StreamWriter which writes to the file</param>
        /// <param name="rows">all methodRowEntries which should be printed</param>
        private static void PrintRowsToFile(
            StreamWriter writer,
            IEnumerable<string> rows)
        {
            foreach (string row in rows)
            {
                writer.WriteLine(row);
            }

            string fullPath = ((FileStream)(writer.BaseStream)).Name;
            Console.WriteLine("\r\nA Report was successfully written to: " + fullPath);
        }

        /// <summary>
        /// prints all items in which the comparison procedure lead to an error to the console.
        /// </summary>
        /// <param name="items">the items which were not successfully processed</param>
        private static void PrintErrorItems(IEnumerable<ServerItemInformation> items)
        {
            if (items.Any())
            {
                Console.WriteLine("\r\n\r\nERRORS in ServerItems: ");

                foreach (ServerItemInformation item in items)
                {
                    Console.WriteLine("  # " + item.FileName);
                }
            }
        }

        /// <summary>
        /// prints the exception message and exits the program
        /// </summary>
        /// <param name="exception">the exception which occurred</param>
        private static void HandleException(Exception exception)
        {
            Console.WriteLine("\r\nAn Error occurred: " + exception.Message);
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