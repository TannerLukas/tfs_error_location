using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace Tfs_Error_Location
{
    /// <summary>
    /// contains the result for the method comparison of the AstComparer.
    /// </summary>
    public class MethodComparisonResult
    {
        /// <summary>
        /// An Enum which defines all possible options of the status of a method.
        /// </summary>
        public enum MethodStatus
        {
            Changed,
            NotChanged,
            Added,
            Deleted
        }

        /// <summary>
        /// Constructor. Inits the Result Property.
        /// </summary>
        public MethodComparisonResult()
        {
            Result = new Dictionary<Method, Dictionary<MethodStatus, int>>();
        }

        /// <summary>
        /// contains the methods as keys. the values consists of all possible states
        /// of a method and a counter which defines how often a specific state was reached.
        /// e.g. Method (notchanged 0, changed 1, added 1, deleted 0)
        /// </summary>
        public Dictionary<Method, Dictionary<MethodStatus, int>> Result
        {
            get;
            private set;
        }

        /// <summary>
        /// adds the method to result and sets the counter of the notchanged status to 1
        /// </summary>
        /// <param name="method">contains the new method definition</param>
        public void AddNotChangedMethod(Method method)
        {
            // if the result does not already contain the method then a corresponding
            // entry is initiliased. 
            if (!Result.ContainsKey(method))
            {
                InitMethodInResult(method);
            }

            Result[method][MethodStatus.NotChanged] = 1;
        }

        /// <summary>
        /// adds the method to result and sets the counter of the changed status to 1
        /// </summary>
        /// <param name="method">contains the new method definition</param>
        public void AddChangedMethod(Method method)
        {
            // if the result does not already contain the method then a corresponding
            // entry is initiliased. 
            if (!Result.ContainsKey(method))
            {
                InitMethodInResult(method);
            }

            Result[method][MethodStatus.Changed] = 1;
        }

        /// <summary>
        /// adds all methods to result and sets the counter of the added status to 1
        /// </summary>
        /// <param name="methods">contains a list of methods which should be added</param>
        public void AddAddedMethods(List<Method> methods)
        {
            foreach (Method method in methods)
            {
                // if the result does not already contain the method then a corresponding
                // entry is initiliased. 
                if (!Result.ContainsKey(method))
                {
                    InitMethodInResult(method);
                }

                Result[method][MethodStatus.Added] = 1;
            }
        }

        /// <summary>
        /// adds all methods to result and sets the counter of the deleted state to 1
        /// </summary>
        /// <param name="methods">contains a list of methods which should be added</param>
        public void AddDeletedMethods(List<Method> methods)
        {
            foreach (Method method in methods)
            {
                // if the result does not already contain the method then a corresponding
                // entry is initiliased. 
                if (!Result.ContainsKey(method))
                {
                    InitMethodInResult(method);
                }

                Result[method][MethodStatus.Deleted] = 1;
            }
        }

        /// <summary>
        /// merges the newResult into the currentResult. if a new method does not 
        /// exist in the current result then it is simply added, otherwise the 
        /// counters for each status are simply added.
        /// e.g. Result(method1(added 1)) newResult(method1(added 1)) => Result(method1(added 2))
        /// </summary>
        /// <param name="newResult">the result which should be merged with the current Result</param>
        public void AggregateMethodResults(MethodComparisonResult newResult)
        {
            foreach (
                KeyValuePair<Method, Dictionary<MethodStatus, int>> methodEntry in newResult.Result)
            {
                Method method = methodEntry.Key;
                Dictionary<MethodStatus, int> statusCounter = methodEntry.Value;

                Method oldMethod = CheckResultContainsMethod(method);

                //if result does not contain the new method then a corresponding result entry
                //has to be initialised
                if (oldMethod == null)
                {
                    InitMethodInResult(method);
                }

                foreach (KeyValuePair<MethodStatus, int> statusPair in statusCounter)
                {
                    MethodStatus status = statusPair.Key;
                    int counter = statusPair.Value;

                    if (oldMethod != null)
                    {
                        //if a result entry for the newMethod was found then their 
                        //status counter simply have to be merged
                        Result[oldMethod][status] = Result[oldMethod][status] + counter;
                    }
                    else
                    {
                        //simply set the counter for the newMethod and the given status
                        Result[method][status] = counter;
                    }
                }
            }
        }

        /// <summary>
        /// creates a dictionary which contains all methods for a given state
        /// </summary>
        /// <returns>a dictionary which uses the method status as the key and a
        /// list of all methods for that given status as values</returns>
        public Dictionary<MethodStatus, List<Method>> GetMethodsForStatus()
        {
            //init the resulting dictionary
            Dictionary<MethodStatus, List<Method>> result =
                new Dictionary<MethodStatus, List<Method>>
                {
                    {MethodStatus.NotChanged, new List<Method>()},
                    {MethodStatus.Changed, new List<Method>()},
                    {MethodStatus.Added, new List<Method>()},
                    {MethodStatus.Deleted, new List<Method>()}
                };

            foreach (KeyValuePair<Method, Dictionary<MethodStatus, int>> methodEntry in Result)
            {
                Method method = methodEntry.Key;
                Dictionary<MethodStatus, int> statusCounter = methodEntry.Value;

                IEnumerable<KeyValuePair<MethodStatus, int>> counters = statusCounter.Where
                    (s => s.Value > 0);

                foreach (KeyValuePair<MethodStatus, int> status in counters)
                {
                    result[status.Key].Add(method);
                }
            }

            return result;
        }

        /// <summary>
        /// resets properties of the methods which are contained in Result
        /// </summary>
        public void ResetMethods()
        {
            foreach (Method method in Result.Keys)
            {
                method.ClearMethodDeclaration();
            }
        }

        /// <summary>
        /// prints a result overview of the methods to a file
        /// </summary>
        /// <param name="writer">Writer used to write the contents to the file</param>
        /// <param name="tableWidth">specifies the width of the output table in the file</param>
        /// <param name="columnSeperator">contains a string which is used to seperate the columns</param>
        /// <param name="printAllMethods">a flag which indicates of all methods should be
        /// printed or only the methods where somehow changed(added/deleted/changed)</param>
        public void PrintResultOverviewToFile(
            StreamWriter writer,
            int tableWidth,
            string columnSeperator,
            bool printAllMethods = false)
        {   
            foreach (KeyValuePair<Method, Dictionary<MethodStatus, int>> keyValuePair in Result)
            {
                Method method = keyValuePair.Key;
                Dictionary<MethodStatus, int> statusCounter = keyValuePair.Value;

                int changedCounter = GetChangedCounter(statusCounter);

                if (printAllMethods || changedCounter > 0)
                {
                    string methodRow = CreateFileReportMethodRow
                        (method.GetMethodName(), tableWidth, columnSeperator,
                            changedCounter);

                    writer.WriteLine(methodRow);
                }
            }

        }

        public void PrintCompleteResultToFile(StreamWriter writer)
        {
            foreach (KeyValuePair<Method, Dictionary<MethodStatus, int>> keyValuePair in Result)
            {
                Method method = keyValuePair.Key;
                Dictionary<MethodStatus, int> statusCounter = keyValuePair.Value;
                writer.WriteLine("--------------------");
                writer.WriteLine("Method: " + method.MethodDecl.Name);
                foreach (KeyValuePair<MethodStatus, int> valuePair in statusCounter)
                {
                    MethodStatus status = valuePair.Key;
                    int counter = valuePair.Value;
                    writer.WriteLine("Status: " + status + " Counter: " + counter);
                }
            }
        }

        /// <summary>
        /// initialises the given method in the result and sets each status counter = 0
        /// </summary>
        /// <param name="method">the method which should be initialised</param>
        private void InitMethodInResult(Method method)
        {
            Result.Add(method, new Dictionary<MethodStatus, int>());
            foreach (MethodStatus status in Enum.GetValues(typeof(MethodStatus)))
            {
                Result[method].Add(status, 0);
            }
        }

        /// <summary>
        /// checks if result already contains the given method
        /// </summary>
        /// <param name="method">the method which should be checked</param>
        /// <returns>on success: the corresponding method in Result, null otherwise</returns>
        private Method CheckResultContainsMethod(Method method)
        {
            IEnumerable<Method> methods = Result.Keys;
            Method matchingMethod = AstComparer.FindMatchingMethodDeclaration(method, methods);

            return matchingMethod;
        }

        /// <summary>
        /// calculates the final changes counter of a method. Therefore
        /// all counters where the method status != NotChanged are added.
        /// </summary>
        /// <param name="statusCounter">foreach method status it contains a counter
        /// which indicates how often this state was reached.</param>
        /// <returns>the final amount of how often the methods was changed.</returns>
        private static int GetChangedCounter(Dictionary<MethodStatus, int> statusCounter)
        {
            int counter = 0;
            foreach (KeyValuePair<MethodStatus, int> statusPair in statusCounter)
            {
                MethodStatus status = statusPair.Key;
                if (status != MethodStatus.NotChanged)
                {
                    counter += statusPair.Value;
                }
            }

            return counter;
        }

        /// <summary>
        /// create a row in the file report for a method
        /// </summary>
        /// <param name="name">the name of the methods</param>
        /// <param name="tableWidth"></param>
        /// <param name="columnSeperator"></param>
        /// <param name="changedCounter"></param>
        /// <returns>a string containing the whole content of a
        /// method (name/changedcounter) in the output table</returns>
        private static string CreateFileReportMethodRow(
            string name,
            int tableWidth,
            string columnSeperator,
            int changedCounter)
        {
            //20 = changedCounter Column length
            int methodPaddingValue = tableWidth - 20;

            string text = columnSeperator + "\t" + name.PadRight(methodPaddingValue) + columnSeperator +
                          new string(' ', 7) + changedCounter;

            string newText = text.PadRight(tableWidth - 1) + columnSeperator;

            return newText;
        }
    }
}

