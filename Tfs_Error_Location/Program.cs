using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using MethodStatus = Tfs_Error_Location.AstComparer.MethodStatus;

namespace Tfs_Error_Location
{
    public class Program
    {
        private const string s_ErrorWrongUsage =
            "Error: Wrong Usage.\r\n" + "Usage: no parameters | oldFileName newFileName";

        private const string s_OutputSeparator = "---------------------------------\r\n";

        private static void Main(string[] args)
        {
            string oldFileName;
            string oldFileContent;
            string newFileName;
            string newFileContent;

            if (args.Count() == 0)
            {
                //for now only with example files
                oldFileName = "oldExampleFile.cs";
                newFileName = "newExampleFile.cs";
                FileProvider.CreateExampleFiles(out oldFileContent, out newFileContent);
            }
            else if (args.Count() == 2)
            {
                oldFileName = args[0];
                newFileName = args[1];

                Stream oldStream = FileProvider.ReadFile(oldFileName);
                Stream newStream = FileProvider.ReadFile(newFileName);

                if (
                    !(CheckStreamNotNull(oldStream, oldFileName) &&
                      CheckStreamNotNull(newStream, newFileName)))
                {
                    return;
                }

                oldFileContent = FileProvider.ReadStreamIntoString(oldStream);
                newFileContent = FileProvider.ReadStreamIntoString(newStream);
            }
            else
            {
                Console.WriteLine(s_ErrorWrongUsage);
                return;
            }

            MemoryStream errorLogStream;
            Dictionary<MethodStatus, List<Method>> methodComparisonResult =
                AstComparer.CompareSyntaxTrees
                    (oldFileContent, oldFileName, newFileContent, newFileName, out errorLogStream);

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

        private static void PrintErrorLogStream(Stream stream)
        {
            stream.Position = 0;
            StreamReader errorReader = new StreamReader(stream);
            string errorMessage = errorReader.ReadToEnd();
            Console.WriteLine(errorMessage);
        }

        private static bool CheckStreamNotNull(
            Stream stream,
            string fileName)
        {
            if (stream != null)
            {
                return true;
            }

            Console.WriteLine("Error: File: {0} could not be read.", fileName);
            return false;
        }

        private static void PrintReport(Dictionary<MethodStatus, List<Method>> methodsWithStatus)
        {
            PrintMethodStatus
                ("Methods with no changes: ", methodsWithStatus[MethodStatus.NotChanged]);
            PrintMethodStatus("Methods with changes: ", methodsWithStatus[MethodStatus.Changed]);
            PrintMethodStatus("Added Methods: ", methodsWithStatus[MethodStatus.Added]);
            PrintMethodStatus("Deleted Methods: ", methodsWithStatus[MethodStatus.Deleted]);
        }

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
