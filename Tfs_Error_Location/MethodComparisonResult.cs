using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.CSharp;

namespace Tfs_Error_Location
{
    /// <summary>
    /// contains the result from the method comparison of the AstComparer.
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

        public void PrintCompleteResultToConsole()
        {
            foreach (KeyValuePair<Method, Dictionary<MethodStatus, int>> keyValuePair in Result)
            {
                Method method = keyValuePair.Key;
                Dictionary<MethodStatus, int> statusCounter = keyValuePair.Value;
                foreach (KeyValuePair<MethodStatus, int> valuePair in statusCounter)
                {
                    MethodStatus status = valuePair.Key;
                    int counter = valuePair.Value;
                    Console.WriteLine("Status: " + status + " Counter: " + counter);
                }
            }
        }

        public void PrintResultOverviewToFile(
            StreamWriter writer,
            int tableWidth,
            string columnSeperator,
            bool printAllMethods = false)
        {
            //20 = changedCounter Column length
            int methodPaddingValue = tableWidth - 20;

            foreach (KeyValuePair<Method, Dictionary<MethodStatus, int>> keyValuePair in Result)
            {
                Method method = keyValuePair.Key;
                Dictionary<MethodStatus, int> statusCounter = keyValuePair.Value;

                int changedCounter = GetChangedCounter(statusCounter);

                if (printAllMethods || changedCounter > 0)
                {
                    string methodRow = CreateFileReportMethodRow
                        (method.MethodDecl.Name, tableWidth, methodPaddingValue, columnSeperator,
                            changedCounter);

                    writer.WriteLine(methodRow);
                }
            }

        }

        private string CreateFileReportMethodRow(
            string name,
            int tableWidth,
            int paddingValue,
            string columnSeperator,
            int changedCounter)
        {
            string text = columnSeperator + "\t" + name.PadRight(paddingValue) + columnSeperator +
                          new string(' ', 7) + changedCounter;

            string newText = text.PadRight(tableWidth - 1) + columnSeperator;

            return newText;
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
            foreach (MethodStatus status in System.Enum.GetValues(typeof(MethodStatus)))
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

        private int GetChangedCounter(Dictionary<MethodStatus, int> statusCounter)
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
    }
}

