using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace MethodComparison
{
    /// <summary>
    /// compares two syntaxtrees in order to find out which methods have changed.
    /// </summary>
    public class MethodComparer
    {
        /// <summary>
        /// An Enum which defines all possible options of the status of method.
        /// </summary>
        internal enum MethodStatus
        {
            Changed,
            NotChanged,
            Added,
            Deleted
        }

        /// <summary>
        /// An Enum which defines all possible types of the analyzed methods.
        /// </summary>
        public enum MethodType
        {
            Method,
            Constructor,
            Property
        }

        public const string s_NameSeperator = ".";

        /// <summary>
        /// Compares two syntax trees:
        /// 1) Creates SyntaxTrees for the given file contents.
        /// 2) get all methodDeclarations of the trees
        /// 3) creates a methodMapping in order to know which methods should be compared
        /// 4) compares the methods where a matching method was found
        /// 5) if no match was found then the method was either added or deleted
        /// </summary>
        /// <param name="oldFileContent">the content of the old file</param>
        /// <param name="oldFileName">the fileName of the old file</param>
        /// <param name="newFileContent">the content of the new file</param>
        /// <param name="newFileName">the fileName of the new file</param>
        /// <param name="errorLogStream">is the stream to which the errors should be written</param>
        /// <returns>on success: a dictionary containing the possible states of methods as keys,
        /// and a list of their corresponding methods as values, null otherwise</returns>
        public static MethodComparisonResult CompareMethods(
            string oldFileContent,
            string oldFileName,
            string newFileContent,
            string newFileName,
            MemoryStream errorLogStream)
        {
            //retrieve the syntaxTrees for the old and the new file
            SyntaxTree oldTree = GetSyntaxTree(oldFileContent, oldFileName);
            SyntaxTree newTree = GetSyntaxTree(newFileContent, newFileName);

            StreamWriter errorLogWriter = new StreamWriter(errorLogStream) {AutoFlush = true};

            if (!CheckSyntaxTrees(oldTree, newTree, errorLogWriter))
            {
                return null;
            }

            //get all methods for both files
            IEnumerable<Method> oldMethods = GetAllMethods(oldTree);
            IEnumerable<Method> newMethods = GetAllMethods(newTree);

            //create a methodMapping
            //this is needed because we need to know which methods should be compared
            //a new location for a method in the code file is ignored
            //renaming is not handled yet
            List<Method> addedMethods;
            List<Method> deletedMethods;

            Dictionary<Method, Method> methodMapping = CreateMethodMapping
                (oldMethods, newMethods, out addedMethods, out deletedMethods);

            MethodComparisonResult result = CompareMatchedMethods(oldTree, newTree, methodMapping);

            //add the added and deleted Method with their corresponding status to result
            result.AddAddedMethods(addedMethods);
            result.AddDeletedMethods(deletedMethods);

            return result;
        }

        /// <summary>
        /// tries to match to methods by their fully qualified names and their signatures.
        /// </summary>
        /// <param name="oldMethod">the method to which a matching method should be found</param>
        /// <param name="newMethods">the list of all MethodDeclarations of the new file</param>
        /// <returns>on success: the matchingMethod, null otherwise</returns>
        public static Method FindMatchingMethodDeclaration(
            Method oldMethod,
            IEnumerable<Method> newMethods)
        {
            string methodName = oldMethod.FullyQualifiedName;

            List<Method> possibleMethods =
                newMethods.Where
                    (m =>
                        m.FullyQualifiedName.Equals(methodName) &&
                        m.MethodType == oldMethod.MethodType).ToList();

            if (possibleMethods.Count == 1)
            {
                //exactly one method with this name exists
                return possibleMethods.First();
            }
            else if (possibleMethods.Count > 1)
            {
                //compare the complete Signature (and parameters)
                string methodSignature = oldMethod.Signature;

                List<Method> signatureMethods =
                    possibleMethods.Where(m => m.Signature.Equals(methodSignature)).ToList();

                if (signatureMethods.Count == 1)
                {
                    return signatureMethods.First();
                }
            }

            return null;
        }

        /// <summary>
        /// creates a new syntaxTree for the given code
        /// </summary>
        /// <param name="code">the code which should be parsed</param>
        /// <param name="fileName">the fileName of the codeContents</param>
        /// <returns>a new syntaxtree based on the contents of the code parameter</returns>
        private static SyntaxTree GetSyntaxTree(
            string code,
            string fileName)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default, fileName);
            return tree;
        }

        /// <summary>
        /// Retrieves all memberDeclarations (method, constructors, properties) from the given SyntaxTree.
        /// For each memberDeclaration a Method object with its properties is created.
        /// </summary>
        /// <param name="tree">The syntaxtree of the parsed C# file.</param>
        /// <returns>A list of all methods in a SyntaxTree</returns>
        private static IEnumerable<Method> GetAllMethods(
            SyntaxTree tree)
        {
            SyntaxNode root = tree.GetRoot();

            IEnumerable<MemberDeclarationSyntax> methods =
                GetAllDescendantsByType<MethodDeclarationSyntax>(root);

            IEnumerable<MemberDeclarationSyntax> constructors =
                GetAllDescendantsByType<ConstructorDeclarationSyntax>(root);

            IEnumerable<MemberDeclarationSyntax> properties =
                GetAllDescendantsByType<PropertyDeclarationSyntax>(root);

            return CreateAllMethods(methods, constructors, properties);
        }

        /// <summary>
        /// Creates for each method, constructor and property that have been obtained from the
        /// SyntaxTree a corresponding method object.
        /// </summary>
        /// <param name="methods">A list which contains all MethodDeclarations of the underlying SyntaxTree.</param>
        /// <param name="constructors">A list which contains all ContstructorDeclarations of the underlying SyntaxTree.</param>
        /// <param name="properties">A list which contains all PropertyDeclarations of the underlying SyntaxTree.</param>
        /// <returns>A list of all newly created method objects.</returns>
        private static IEnumerable<Method> CreateAllMethods(
            IEnumerable<MemberDeclarationSyntax> methods,
            IEnumerable<MemberDeclarationSyntax> constructors,
            IEnumerable<MemberDeclarationSyntax> properties)
        {
            IEnumerable<Method> newMethods = CreateMethodsForType(methods, MethodType.Method);
            IEnumerable<Method> newConstructors = CreateMethodsForType
                (constructors, MethodType.Constructor);
            IEnumerable<Method> newProperties = CreateMethodsForType
                (properties, MethodType.Property);

            return ConcatenateIEnumerable(newMethods, newConstructors, newProperties);
        }

        /// <summary>
        /// Creates the corresponding method objects for the given type.
        /// </summary>
        /// <param name="methods">A list of methods (methods, constructor, property)</param>
        /// <param name="methodType">Defines the specific type for the new method objects.</param>
        /// <returns>A list of all newly created method objects.</returns>
        private static IEnumerable<Method> CreateMethodsForType(
            IEnumerable<MemberDeclarationSyntax> methods,
            MethodType methodType)
        {
            List<Method> newMethods = new List<Method>();

            foreach (var method in methods)
            {
                //retrieve the typeName and namespace in which the method is contained
                string typeName;
                string namespaceName;
                GetNamespaceAndClassName(method, out typeName, out namespaceName);

                //string signature = GetMethodSignatureString(methodDeclaration);
                string signatureWithParameters = GetMethodSignatureWithParameters(method);

                //create and add the new method definition
                newMethods.Add
                    (new Method
                        (method.GetLocation(), namespaceName, typeName, GetMethodName(method),
                            s_NameSeperator, signatureWithParameters, methodType));
            }

            return newMethods;
        }

        /// <summary>
        /// returns a list containing all Descendants of type T of a specific node in the astTree
        /// </summary>
        /// <typeparam name="T">defines the type from which all descandants should be retrieved</typeparam>
        /// <param name="node">an AstNode in a SyntaxTree</param>
        /// <returns>an IEnumerable containing all Descendants of type T in the tree</returns>
        private static IEnumerable<T> GetAllDescendantsByType<T>(
            SyntaxNode node)
        {
            return node.DescendantNodes().OfType<T>();
        }

        /// <summary>
        /// concatenates multiple IEnumerables for a specific type
        /// </summary>
        /// <typeparam name="T">defines the type</typeparam>
        /// <param name="lists">list of IEnumerables which should be concatenated</param>
        /// <returns>a list containing all elements of the given parameters</returns>
        private static IEnumerable<T> ConcatenateIEnumerable<T>(
            params IEnumerable<T>[] lists)
        {
            return lists.SelectMany(x => x);
        }

        /// <summary>
        /// Returns the name of the member if it is of type method/constructor/property.
        /// </summary>
        /// <param name="member">The member for which its name should be retrieved.</param>
        /// <returns>On success: The name of the method, an empty string otherwise</returns>
        private static string GetMethodName(
            MemberDeclarationSyntax member)
        {
            string name = String.Empty;

            //Those types have to be distinguished because their Base methods
            //do not contain any implementation for the identifier

            MethodDeclarationSyntax md = member as MethodDeclarationSyntax;
            if (md != null)
            {
                name = md.Identifier.Value.ToString();
            }
            else
            {
                ConstructorDeclarationSyntax cd = member as ConstructorDeclarationSyntax;
                if (cd != null)
                {
                    name = cd.Identifier.Value.ToString();
                }
                else
                {
                    PropertyDeclarationSyntax property = member as PropertyDeclarationSyntax;
                    if (property != null)
                    {
                        name = property.Identifier.Value.ToString();
                    }
                }
            }

            return name;
        }

        /// <summary>
        /// Creates the full typeName and namespaceName for the given method. if the method
        /// is contained in sub class/namespace definitions their names are aggregated.
        /// e.g. typeName = class1.class2
        /// </summary>
        /// <param name="method">the method which should be analyzed</param>
        /// <param name="typeName">the type definition of the method</param>
        /// <param name="namespaceName">the namespace definition of the method</param>
        private static void GetNamespaceAndClassName(
            SyntaxNode method,
            out string typeName,
            out string namespaceName)
        {
            List<string> typeNames = new List<string>();
            List<string> namespaceNames = new List<string>();

            SyntaxNode parent = method.Parent;

            while (parent != null)
            {
                string className = GetClassName(parent);
                if (!className.Equals(String.Empty))
                {
                    typeNames.Insert(0, className);
                }
                else
                {
                    string namespaceString = GetNamespaceName(parent);
                    if (!namespaceString.Equals(String.Empty))
                    {
                        namespaceNames.Insert(0, namespaceString);
                    }
                }
                
                parent = parent.Parent;
            }

            typeName = string.Join(s_NameSeperator, typeNames);
            namespaceName = string.Join(s_NameSeperator, namespaceNames);
        }

        private static string GetClassName(SyntaxNode node)
        {
            string className = String.Empty;

            TypeDeclarationSyntax typeNode = node as TypeDeclarationSyntax;
            if (typeNode != null)
            {
                className = typeNode.Identifier.Value.ToString();
            }

            return className;
        }

        private static string GetNamespaceName(SyntaxNode node)
        {
            string namespaceName = String.Empty;

            NamespaceDeclarationSyntax namespaceNode = node as NamespaceDeclarationSyntax;
            if (namespaceNode != null)
            {
                namespaceName = namespaceNode.Name.NormalizeWhitespace().ToString();
            }

            return namespaceName;
        }

        /// <summary>
        /// Creates a methodMapping by comparing the fully qualified names and the signature
        /// of the methods. The methods were no mapping was found are either added or deleted.
        /// </summary>
        /// <param name="oldMethods">a list of methods in the oldFile</param>
        /// <param name="newMethods">a list of methods in the newFile</param>
        /// <param name="addedMethods">contains the methods which were added</param>
        /// <param name="deletedMethods">contains the methods which were deleted</param>
        /// <returns>a dictionary of matching methods</returns>
        private static Dictionary<Method, Method> CreateMethodMapping(
            IEnumerable<Method> oldMethods,
            IEnumerable<Method> newMethods,
            out List<Method> addedMethods,
            out List<Method> deletedMethods)
        {
            Dictionary<Method, Method> methodMapping = new Dictionary<Method, Method>();
            deletedMethods = new List<Method>();
            List<Method> newMethodsList = newMethods.ToList();

            foreach (Method oldMethod in oldMethods)
            {
                //try to find the old method declaration in the new file
                Method matchingMethod = FindMatchingMethodDeclaration(oldMethod, newMethodsList);

                if (matchingMethod != null)
                {
                    //indicates that a matching was found
                    methodMapping.Add(oldMethod, matchingMethod);
                }
                else
                {
                    //the old method was not found in the new file
                    //hence it was deleted
                    deletedMethods.Add(oldMethod);
                }
            }

            //find all methods which were added in the new file
            addedMethods = FindAllNewMethodDeclarations(methodMapping.Values, newMethodsList);

            return methodMapping;
        }

        /// <summary>
        /// checks for which methods no mapping exists, because this indicates that they
        /// were added
        /// </summary>
        /// <param name="mappedMethods">the list of methods where a mapping was already found</param>
        /// <param name="newMethods">the list of all MethodDeclarations of the new file</param>
        /// <returns>a list of methods which were added in the new file</returns>
        private static List<Method> FindAllNewMethodDeclarations(
            IEnumerable<Method> mappedMethods,
            IEnumerable<Method> newMethods)
        {
            List<Method> addedMethods = new List<Method>();

            foreach (Method newMethod in newMethods)
            {
                //a non existing mapping indicates that the method was added 
                if (!mappedMethods.Contains(newMethod))
                {
                    addedMethods.Add(newMethod);
                }
            }

            return addedMethods;
        }

        private static SyntaxNode FindMethodDeclarationViaLocation(
            SyntaxTree tree,
            Method method)
        {
            return tree.GetRoot().FindNode(method.StartLocation.SourceSpan);
        }

        /// <summary>
        /// Compares the methods to which a mapping was found in the following way:
        /// Traverse through the childNodes of both Methods and compare them.
        /// </summary>
        /// <param name="oldTree">The syntraxtree of the old file.</param>
        /// <param name="newTree">The syntaxtree of the new file.</param>
        /// <param name="methodMapping">a mapping of the matching methods</param>
        /// <returns>a dictionary containing the changedmethods and the 
        /// methods which were not changed.</returns>
        private static MethodComparisonResult CompareMatchedMethods(
            SyntaxTree oldTree,
            SyntaxTree newTree,
            Dictionary<Method, Method> methodMapping)
        {
            MethodComparisonResult result = new MethodComparisonResult();

            foreach (KeyValuePair<Method, Method> pair in methodMapping)
            {
                Method oldMethod = pair.Key;
                Method newMethod = pair.Value;

                //get the corresponding methodDeclarations by accessing the via their StartLocation
                SyntaxNode oldDeclaration = FindMethodDeclarationViaLocation(oldTree, oldMethod);
                SyntaxNode newDeclaration = FindMethodDeclarationViaLocation(newTree, newMethod);

                //traverses through the methods children and compares them
                //if a change is found, the current SyntaxNode or Token is returned
                SyntaxNodeOrToken changeNodeOrToken = TraverseChildNodesAndTokens
                    (oldDeclaration.ChildNodesAndTokens(), newDeclaration.ChildNodesAndTokens());

                if (changeNodeOrToken != null)
                {
                    newMethod.SetChangeEntryLocation(changeNodeOrToken);
                    result.AddChangedMethod(newMethod);
                }
                else
                {
                    result.AddNotChangedMethod(newMethod);
                }
            }

            return result;
        }

        /// <summary>
        /// Compares all oldChildNodes(and tokens) with all newChildNodes(and tokens). 
        /// If a child has children then this method will be called again for the 
        /// childs.Children (recursion). All comments and whitespaces are ignored.
        /// </summary>
        /// <param name="oldChildren">contains the old childNodes and tokens</param>
        /// <param name="newChildren">contains the new childNodes and tokens</param>
        /// <returns>if a changes was found: the SyntaxNode or token where the first 
        /// change is detected, null otherwise</returns>
        private static SyntaxNodeOrToken TraverseChildNodesAndTokens(
            ChildSyntaxList oldChildren,
            ChildSyntaxList newChildren)
        {
            int counter = 0;
            SyntaxNodeOrToken change = null;
            SyntaxNodeOrToken newChild = null;

            foreach (SyntaxNodeOrToken oldChild in oldChildren)
            {
                if (counter >= newChildren.Count)
                {
                    return newChild;
                }

                newChild = newChildren.ElementAt(counter);

                ChildSyntaxList oldChildChildren = oldChild.ChildNodesAndTokens();
                ChildSyntaxList newChildChildren = newChild.ChildNodesAndTokens();

                //if the children also has children then the same method 
                //is called for the child.children
                //otherwise the text of the current old and new node is compared
                if (oldChildChildren.Any())
                {
                    if (!newChildChildren.Any())
                    {
                        change = newChild;
                        return change;
                    }

                    change = TraverseChildNodesAndTokens(oldChildChildren, newChildChildren);
                    if (change != null)
                    {
                        return change;
                    }
                }
                else
                {
                    string old = GetFilteredSyntaxNodeText(oldChild);
                    string newText = GetFilteredSyntaxNodeText(newChild);

                    if (!old.Equals(newText))
                    {
                        change = newChild;
                        return change;
                    }
                }

                counter++;
            }

            return change;
        }

        private static string GetFilteredSyntaxNodeText(
            SyntaxNodeOrToken node)
        {
            string filteredText = String.Empty;

            SyntaxNode syntaxNode = node.AsNode();

            if (syntaxNode != null)
            {
                //removes whitespaces
                filteredText = syntaxNode.NormalizeWhitespace().ToString();
            }
            else
            {
                filteredText = node.AsToken().ValueText;
            }

            return filteredText;
        }

        /// <summary>
        /// Creates a method signature string including all parameters.
        /// e.g. public static void Main(param1,param2)
        /// e.g. public Name, for a property
        /// </summary>
        /// <param name="method">the method for which the signature should be created</param>
        /// <returns>a string of the method signature(modifers, returnValue, name, (parameters))</returns>
        private static string GetMethodSignatureWithParameters(
            MemberDeclarationSyntax method)
        {
            string signature = String.Empty;
            string modifierString = String.Empty;
            string returnType = String.Empty;
            string name = GetMethodName(method);

            //either a method or a constructor
            BaseMethodDeclarationSyntax m = method as BaseMethodDeclarationSyntax;

            if (m != null)
            {
                modifierString = GetModifierString(m.Modifiers);
                returnType = GetReturnTypeString(m);
                string parameters = GetParametersAsStrings(m);

                signature = modifierString + returnType + name + "(" + parameters + ")";
            }
            else
            {
                //check for property 
                PropertyDeclarationSyntax property = method as PropertyDeclarationSyntax;

                if (property != null)
                {
                    modifierString = GetModifierString(property.Modifiers);
                    signature = modifierString + name;
                }
            }

            return signature;
        }

        private static string GetModifierString(
            SyntaxTokenList modifiers)
        {
            string modifierString = String.Empty;

            foreach (var modifier in modifiers)
            {
                modifierString += modifier.Text + " ";
            }

            return modifierString;
        }

        private static string GetReturnTypeString(
            BaseMethodDeclarationSyntax bmd)
        {
            string returnType = String.Empty;

            MethodDeclarationSyntax md = bmd as MethodDeclarationSyntax;
            if (md != null)
            {
                //is an ordinary method: has a return type
                returnType = md.ReturnType + " ";
            }

            return returnType;
        }

        /// <summary>
        /// creates a string which contains all parameters of a method 
        /// seperated by a ','
        /// </summary>
        /// <param name="method">the method from which the parameter string should 
        /// be created</param>
        /// <returns>a string containing all parameters, an empty string
        /// if the method does not contain any parameters</returns>
        private static string GetParametersAsStrings(
            BaseMethodDeclarationSyntax method)
        {
            string parameterString = String.Empty;

            IEnumerable<ParameterSyntax> parameters = method.ParameterList.Parameters;

            foreach (ParameterSyntax param in parameters)
            {
                parameterString += param + ",";
            }

            if (!parameterString.Equals(String.Empty))
            {
                // remove the ',' after the last parameter
                parameterString = parameterString.Substring(0, parameterString.Length - 1);
            }

            return parameterString;
        }

        /// <summary>
        /// checks if the two trees were created successfully (without parsing errors)
        /// </summary>
        /// <param name="oldTree">is the syntaxTree for the old file content</param>
        /// <param name="newTree">is the syntaxTree for the new file content</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>true if no errors occurred, false otherwise</returns>
        private static bool CheckSyntaxTrees(
            SyntaxTree oldTree,
            SyntaxTree newTree,
            StreamWriter errorLogWriter)
        {
            bool result = CheckSyntaxTreeErrors(oldTree, errorLogWriter) &&
                          CheckSyntaxTreeErrors(newTree, errorLogWriter);

            return result;
        }

        /// <summary>
        /// checks if any syntax errors occurred in the syntaxTree.
        /// prints the errors to the errorLogStream.
        /// </summary>
        /// <param name="tree">the underlying syntaxtree to check</param>
        /// <param name="errorLogWriter">StreamWriter used for all errors</param>
        /// <returns>true if no errors occurred, false otherwise</returns>
        private static bool CheckSyntaxTreeErrors(
            SyntaxTree tree,
            StreamWriter errorLogWriter)
        {
            //ignore warnings
            IEnumerable<Diagnostic> errors = tree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error);

            if (errors.Any())
            {
                foreach (Diagnostic error in errors)
                {
                    errorLogWriter.WriteLine("Error : " + error);
                }

                return false;
            }

            return true;
        }
    }
}
