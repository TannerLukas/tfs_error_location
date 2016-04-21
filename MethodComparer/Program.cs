using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MethodStatus = MethodComparison.MethodComparisonResult.MethodStatus;

namespace MethodComparison
{
    class Program
    {
        private const string s_ErrorWrongUsage =
            "Error: Wrong Usage.\r\n" + "Usage: MethodComparer.exe oldFile newFile";

        private const string s_OutputSeparator = "---------------------------------\r\n";

        private const int s_OldFileIndex = 0;
        private const int s_NewFileIndex = 1;
        private const int s_ExpectedParametersCount = 2;

        private static void Main(string[] args)
        {
            if (args.Length != s_ExpectedParametersCount)
            {
                Console.WriteLine(s_ErrorWrongUsage);
            }
            else
            {
                string oldFile = args[s_OldFileIndex];
                string newFile = args[s_NewFileIndex];

                string oldFileContent = ReadFile(oldFile);
                string newFileContent = ReadFile(newFile);

                if (oldFileContent != null && newFileContent != null)
                {
                    CompareFileContents(oldFile, oldFileContent, newFile, newFileContent);
                }

            }
        }

        /// <summary>
        /// reads the contents of a file into a string.
        /// </summary>
        /// <param name="fileName">contains the path to the file</param>
        /// <returns>on success: a stream containing the contents of the file,
        /// null otherwise</returns>
        private static string ReadFile(
            string fileName)
        {
            try
            {
                //check if File exists
                if (File.Exists(fileName))
                {
                    using (StreamReader reader = new StreamReader(fileName))
                    {
                        string fileContent = reader.ReadToEnd();
                        return fileContent;
                    }
                }
                else
                {
                    Console.WriteLine("Error: the file " + fileName + " does not exist.");
                }
            }
            catch (IOException exception)
            {
                Console.WriteLine(exception.Message);
            }

            return null;
        }


        private static void CompareFileContents(
            string oldFileName, 
            string oldFileContent,
            string newFileName,
            string newFileContent
            )
        {
            if (oldFileContent == null ||
                newFileContent == null)
            {
                return;
            }

            using (MemoryStream errorLogStream = new MemoryStream())
            {
                MethodComparisonResult methodComparisonResult = MethodComparer.CompareSyntaxTrees
                    (oldFileContent, oldFileName, newFileContent, newFileName, errorLogStream);

                if (methodComparisonResult != null)
                {
                    //just for debug purposes
                    PrintReport(methodComparisonResult);
                }
                else
                {
                    //errors occured
                    PrintErrorLogStream(errorLogStream);
                }
            }
        }

        /// <summary>
        /// prints the errors to the console
        /// </summary>
        /// <param name="stream">contains the error messages</param>
        private static void PrintErrorLogStream(Stream stream)
        {
            stream.Position = 0;
            StreamReader errorReader = new StreamReader(stream);
            string errorMessage = errorReader.ReadToEnd();
            Console.WriteLine(errorMessage);
        }

        /// <summary>
        /// prints the final report of the method comparison result
        /// </summary>
        /// <param name="methodComparison">the result of the AstComparer</param>
        private static void PrintReport(MethodComparisonResult methodComparison)
        {
            Dictionary<MethodStatus, List<Method>> statusResult =
                methodComparison.GetMethodsForStatus();

            PrintMethodStatus("Methods with no changes: ", statusResult[MethodStatus.NotChanged]);
            PrintMethodStatus("Methods with changes: ", statusResult[MethodStatus.Changed]);
            PrintMethodStatus("Added Methods: ", statusResult[MethodStatus.Added]);
            PrintMethodStatus("Deleted Methods: ", statusResult[MethodStatus.Deleted]);
        }

        /// <summary>
        /// prints the fully qualified name of the methods
        /// </summary>
        /// <param name="headLine">contains the headLine for the methods</param>
        /// <param name="methods">contains the methods which should be printed</param>
        private static void PrintMethodStatus(
            string headLine,
            List<Method> methods)
        {
            Console.WriteLine(headLine);
            if (methods.Any())
            {
                foreach (Method method in methods)
                {
                    Console.WriteLine(method.FullyQualifiedName);
                }
            }
            else
            {
                Console.WriteLine("none");
            }
            Console.WriteLine(s_OutputSeparator);
        }
    }
}
