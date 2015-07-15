using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using MethodStatus = Tfs_Error_Location.MethodComparisonResult.MethodStatus;

namespace Tfs_Error_Location
{
    class Program
    {
        private const string s_ErrorWrongUsage =
            "Error: Wrong Usage.\r\n" + "Usage: no parameters | oldFileName newFileName";

        private const string s_OutputSeparator = "---------------------------------\r\n";

        private const string s_ExecuteExamples = "The program is executed for example files.\r\n";

        private static void Main(string[] args)
        {
            string oldFileName;
            string oldFileContent;
            string newFileName;
            string newFileContent;

            if (args.Count() == 0)
            {
                Console.WriteLine(s_ExecuteExamples);
                //for now only with example files
                oldFileName = "oldExampleFile.cs";
                newFileName = "newExampleFile.cs";
                FileProvider.CreateExampleFiles(out oldFileContent, out newFileContent);
            }
            else if (args.Count() == 2)
            {
                oldFileName = args[0];
                newFileName = args[1];
                oldFileContent = FileProvider.ReadFile(oldFileName);
                newFileContent = FileProvider.ReadFile(newFileName);
            }
            else
            {
                Console.WriteLine(s_ErrorWrongUsage);
                return;
            }

            if (oldFileContent == null ||
                newFileContent == null)
            {
                //an error occured
                Console.WriteLine("An error occurred during the reading of a file.");
                return;
            }

            using (MemoryStream errorLogStream = new MemoryStream())
            {
                MethodComparisonResult methodComparisonResult = AstComparer.CompareSyntaxTrees
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
