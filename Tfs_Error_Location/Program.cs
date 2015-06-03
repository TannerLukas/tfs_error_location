using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.CSharp.Resolver;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using MethodStatus = Tfs_Error_Location.AstComparer.MethodStatus;


namespace Tfs_Error_Location
{
    class Program
    {
        static void Main(string[] args)
        {
            //get files (fileprovider) via changeset
            //for now only with example files
            string oldFile, newFile;
            FileProvider.GetFiles(out oldFile, out newFile);

            //get their syntax trees
            SyntaxTree oldTree = GetSyntaxTree(oldFile);
            SyntaxTree newTree = GetSyntaxTree(newFile);

            //compare them
            Dictionary<MethodStatus, List<MethodDeclaration>> methodComparisonResult = 
                AstComparer.CompareSyntaxTrees(oldTree, newTree);

            //just for debug purposes
            PrintReport(methodComparisonResult);
        }

        private static void PrintReport(Dictionary<MethodStatus, List<MethodDeclaration>> methodsWithStatus)
        {
            Console.WriteLine("Methods with no changes: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.NotChanged])
            {
                Console.WriteLine(method.Name);
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Methods with changes: ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Changed])
            {
                Console.WriteLine(method.Name);
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Added Methods");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Added])
            {
                Console.WriteLine(method.Name);
            }

            Console.WriteLine("---------------------------");
            Console.WriteLine("Deleted Methods ");
            foreach (MethodDeclaration method in methodsWithStatus[MethodStatus.Deleted])
            {
                Console.WriteLine(method.Name);
            }


        }

        private static SyntaxTree GetSyntaxTree(string code)
        {
            SyntaxTree tree = new CSharpParser().Parse(code);
            return tree;
        }
    }
}
