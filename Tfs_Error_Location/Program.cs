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
            Dictionary<MethodStatus, List<MethodDeclaration>> methodComparisonResult =
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

        private static void PrintReport(
            Dictionary<MethodStatus, List<MethodDeclaration>> methodsWithStatus)
        {
            Console.WriteLine("Methods with no changes: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.NotChanged])
            {
                Console.WriteLine(AstComparer.GetFullyQualifiedMethodName(method));
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Methods with changes: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Changed])
            {
                Console.WriteLine(AstComparer.GetFullyQualifiedMethodName(method));
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Added Methods: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Added])
            {
                Console.WriteLine(AstComparer.GetFullyQualifiedMethodName(method));
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Deleted Methods: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Deleted])
            {
                Console.WriteLine(AstComparer.GetFullyQualifiedMethodName(method));
            }
        }
    }
}
