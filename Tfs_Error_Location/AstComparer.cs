using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.Cecil;
using Mono.CSharp;
using CSharpParser = ICSharpCode.NRefactory.CSharp.CSharpParser;
using Modifiers = ICSharpCode.NRefactory.CSharp.Modifiers;
using ParameterModifier = ICSharpCode.NRefactory.CSharp.ParameterModifier;

namespace Tfs_Error_Location
{
    /// <summary>
    /// compares two syntaxtrees in order to find out which methods have changed.
    /// </summary>
    public class AstComparer
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

        private const string s_CommentString = "Comment";

        /// <summary>
        /// Compares two syntax trees:
        /// 1) get all methodDeclarations of the trees
        /// 2) creates a methodMapping in order to know which methods should be compared
        /// 3) compares the methods where a matching method was found
        /// 4) if no match was found then the method was either added or deleted
        /// </summary>
        /// <param name="oldFileContent">the content of the old file</param>
        /// <param name="oldFileName">the fileName of the old file</param>
        /// <param name="newFileContent">the content of the new file</param>
        /// <param name="newFileName">the fileName of the new file</param>
        /// <param name="errorLogStream">is the stream to which the errors should be written</param>
        /// <param name="ignoreComments">A flag which indicates if changes to comments 
        /// should also be taken into account when changes of a method are calculated.
        /// If it is true, all comments are ignored.</param>
        /// <returns>on success: a dictionary containing the possible states of methods as keys,
        /// and a list of their corresponding methods as values, null otherwise</returns>
        public static MethodComparisonResult CompareSyntaxTrees(
            string oldFileContent,
            string oldFileName,
            string newFileContent,
            string newFileName,
            MemoryStream errorLogStream,
            bool ignoreComments = true)
        {
            //retrieve the syntaxTrees for the old and the new file
            SyntaxTree oldTree = GetSyntaxTree(oldFileContent, oldFileName);
            SyntaxTree newTree = GetSyntaxTree(newFileContent, newFileName);

            StreamWriter errorLogWriter = new StreamWriter(errorLogStream);

            if (CheckSyntaxTreeErrors(oldTree, errorLogWriter) == false ||
                CheckSyntaxTreeErrors(newTree, errorLogWriter) == false)
            {
                return null;
            }

            //get all methods for both files
            IEnumerable<Method> oldMethods = GetAllMethodDeclarations(oldTree);
            IEnumerable<Method> newMethods = GetAllMethodDeclarations(newTree);

            //create a methodMapping
            //this is needed because we need to know which methods should be compared
            //a new location for a method in the code file is ignored
            //renaming is not handled yet
            List<Method> addedMethods;
            List<Method> deletedMethods;
            Dictionary<Method, Method> methodMapping = CreateMethodMapping
                (oldMethods, newMethods, out addedMethods, out deletedMethods);

            MethodComparisonResult result = CompareMatchedMethods(methodMapping, ignoreComments);

            //add the added and deleted Method with their corresponding status to result
            result.AddAddedMethods(addedMethods);
            result.AddDeletedMethods(deletedMethods);

            return result;
        }

        /// <summary>
        /// try to find a methodDeclaration in the newMethodDeclarations.
        /// the comparison done by comparing the fully qualified names. if more
        /// than one method exists with this name (overloading), then those methods
        /// are compared via their signature string with parameters.
        /// </summary>
        /// <param name="oldMethod">the method to which a matching method should be found</param>
        /// <param name="newMethods">the list of all MethodDeclarations of the new file</param>
        /// <returns>on success: the matchingMethod, null otherwise</returns>
        public static Method FindMatchingMethodDeclaration(
            Method oldMethod,
            IEnumerable<Method> newMethods)
        {
            string methodName = oldMethod.FullyQualifiedName;

            IEnumerable<Method> possibleMethods = newMethods.Where
                (m => m.FullyQualifiedName.Equals(methodName));

            if (possibleMethods.Count() == 1)
            {
                //exactly one method with this name exists
                return possibleMethods.First();
            }
            else if (possibleMethods.Count() > 1)
            {
                //compare the complete Signature (and parameters)
                string methodSignature = oldMethod.SignatureWithParameters;

                IEnumerable<Method> signatureMethods = possibleMethods.Where
                    (m => m.SignatureWithParameters.Equals(methodSignature));

                if (signatureMethods.Count() == 1)
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
            SyntaxTree tree = new CSharpParser().Parse(code, fileName);
            return tree;
        }

        /// <summary>
        /// retrieves all methodDeclarations from the given SyntaxTree.
        /// for each methodDeclaration a Method object with its properties is created
        /// </summary>
        /// <param name="tree">the tree from which the methods should be retrieved</param>
        /// <returns>a list of all methods in a SyntaxTree</returns>
        private static IEnumerable<Method> GetAllMethodDeclarations(SyntaxTree tree)
        {
            IEnumerable<MethodDeclaration> methodDeclarations =
                tree.Descendants.OfType<MethodDeclaration>();
            List<Method> methods = new List<Method>();

            foreach (MethodDeclaration methodDeclaration in methodDeclarations)
            {
                //a method contains the methodDeclaration, the fully qualified method name
                //the method signature and a list of all changed astNodes
                string fullName = GetFullyQualifiedMethodName(methodDeclaration);
                string signature = GetMethodSignatureString(methodDeclaration);
                string signatureWithParameters = GetMethodSignatureWithParameters(methodDeclaration);

                methods.Add
                    (new Method
                        (methodDeclaration, new List<AstNode>(), fullName, signature,
                            signatureWithParameters));
            }

            return methods;
        }

        /// <summary>
        /// creates the fully classified methodName of a method.
        /// (namespace.type1.type2.methodName)
        /// </summary>
        /// <param name="method">the method for which the name should be returned</param>
        /// <returns>the fully classified methodName</returns>
        private static string GetFullyQualifiedMethodName(MethodDeclaration method)
        {
            TypeDeclaration classType = method.GetParent<TypeDeclaration>();
            AstNode nextParent = classType.Parent;

            string fullClassName = classType.Name + "." + method.Name;

            //a class could be defined within a class
            //therefore, each class name is added to the fully qualified name
            //finally the namespace is added
            while (nextParent != null)
            {
                if (nextParent.GetType() == typeof(TypeDeclaration))
                {
                    fullClassName = ((TypeDeclaration)nextParent).Name + "." + fullClassName;
                }
                else
                {
                    //reached the namespace declaration
                    fullClassName = ((NamespaceDeclaration)nextParent).Name + "." + fullClassName;
                    return fullClassName;
                }

                nextParent = nextParent.Parent;
            }

            return fullClassName;
        }

        /// <summary>
        /// Create a methodMapping by comparing the names of methods.
        /// The methods were no mapping was found are either added or deleted.
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

        /// <summary>
        /// compares the methods to which a mapping was found in the following way:
        /// traverse through the childNodes of both Methods and compare them
        /// </summary>
        /// <param name="methodMapping">a mapping of the matching methods</param>
        /// <param name="ignoreComments">indicates if comments should be taken into
        /// account while calculating the methodcomparison result</param>
        /// <returns>a dictionary containing the changedmethods and the 
        /// methods which were not changed.</returns>
        private static MethodComparisonResult CompareMatchedMethods(
            Dictionary<Method, Method> methodMapping,
            bool ignoreComments)
        {
            MethodComparisonResult result = new MethodComparisonResult();

            foreach (KeyValuePair<Method, Method> pair in methodMapping)
            {
                Method oldMethod = pair.Key;
                Method newMethod = pair.Value;
                /*
                CompareMethodModifiers(oldMethod, newMethod) &&
                CompareReturnValueType(oldMethod, newMethod) &&
                CompareParameters(oldMethod, newMethod)
                 * */
                AstNode changeNode = CompareMethods
                    (oldMethod.MethodDecl, newMethod.MethodDecl, ignoreComments);

                if (changeNode != null)
                {
                    newMethod.AddChangeNode(changeNode);
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
        /// compares the modifiers of two methods. As the Modifier Type is
        /// an enum they could be compared directly.
        /// </summary>
        /// <param name="oldMethod">contains the old MethodDeclaration</param>
        /// <param name="newMethod">contains the new MethodDeclaration</param>
        /// <returns>true if the methods have the same modifiers, false otherwise</returns>
        private static bool CompareMethodModifiers(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            Modifiers oldModifiers = oldMethod.Modifiers;
            Modifiers newModifiers = newMethod.Modifiers;

            return (oldModifiers == newModifiers);
        }

        /// <summary>
        /// compares the returnValueType of two methods. As the ReturnValueType is
        /// an AstType it does not contain any method for comparison. Hence, only
        /// the text contents are compared.
        /// </summary>
        /// <param name="oldMethod">contains the old MethodDeclaration</param>
        /// <param name="newMethod">contains the new MethodDeclaration</param>
        /// <returns>true if the methods have the same returnValueType, false otherwise</returns>
        private static bool CompareReturnValueType(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            AstType oldReturnType = oldMethod.ReturnType;
            AstType newReturnType = newMethod.ReturnType;

            string oldType = oldReturnType.ToString();
            string newType = newReturnType.ToString();

            return (oldType.Equals(newType));
        }

        /// <summary>
        /// compares the parameters of two methods
        /// </summary>
        /// <param name="oldMethod">contains the old MethodDeclaration</param>
        /// <param name="newMethod">contains the new MethodDeclaration</param>
        /// <returns>true if the methods have the same parameters, false otherwise</returns>
        private static bool CompareParameters(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            AstNodeCollection<ParameterDeclaration> oldParameters = oldMethod.Parameters;
            AstNodeCollection<ParameterDeclaration> newParameters = newMethod.Parameters;

            if (oldParameters.Count != newParameters.Count)
            {
                return false;
            }

            int counter = 0;
            foreach (ParameterDeclaration parameter in oldParameters)
            {
                //A Parameter consists of a name, type and a modifier

                //retrieve the corresponding attributes from the oldParameter
                string oldParameterName;
                ParameterModifier oldParameterModifier;
                string oldParameterType;

                GetParameterAttributes
                    (parameter, out oldParameterName, out oldParameterType, out oldParameterModifier);

                //retrieve the corresponding attributes from the newParameter
                ParameterDeclaration newParameter = newParameters.ElementAt(counter);
                string newParameterName;
                ParameterModifier newParameterModifier;
                string newParameterType;

                GetParameterAttributes
                    (newParameter, out newParameterName, out newParameterType,
                        out newParameterModifier);

                //compare their attributes
                bool result = (oldParameterName.Equals(newParameterName)) &&
                              (oldParameterModifier == newParameterModifier) &&
                              (oldParameterType.Equals(newParameterType));

                if (!result)
                {
                    return false;
                }

                counter++;
            }

            return true;
        }

        /// <summary>
        /// retrieves several attributes from a parameter
        /// </summary>
        /// <param name="parameter">the parameter from which the attributes 
        /// should be returned</param>
        /// <param name="parameterName">contains the name of the parameter</param>
        /// <param name="parameterType">contains the type of the parameter as a string</param>
        /// <param name="parameterModifier">contains the name of the parameter</param>
        private static void GetParameterAttributes(
            ParameterDeclaration parameter,
            out string parameterName,
            out string parameterType,
            out ParameterModifier parameterModifier)
        {
            parameterName = parameter.Name;
            parameterModifier = parameter.ParameterModifier;
            parameterType = parameter.Type.ToString();
        }

        /// <summary>
        /// 1) retrieves all childnodes from the method
        /// 2) traverses through their children and compares them
        /// </summary>
        /// <param name="oldMethod">contains the old MethodDeclaration</param>
        /// <param name="newMethod">contains the new MethodDeclaration</param>
        /// <param name="ignoreComments">indicates if comments should be taken into
        /// account while calculating the methodcomparison result</param>
        /// <returns>true if the methods have the same body, false otherwise</returns>
        private static AstNode CompareMethods(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod,
            bool ignoreComments)
        {
            AstNode change;

            //Comments are ignored if s_IgnoreComments == true
            //otherwise, comment changes indicate that the method has changed.
            if (ignoreComments)
            {
                change = TraverseMethodBodyIgnoringComments(oldMethod.Children, newMethod.Children);
            }
            else
            {
                change = TraverseMethodBody(oldMethod.Children, newMethod.Children);
            }

            return change;
        }

        /// <summary>
        /// Compares all oldChildNodes with all newChildNodes.
        /// if a child has also any children then this method will again 
        /// be called with the childs.Children (recursion). All comments 
        /// are ignored.
        /// </summary>
        /// <param name="oldChildren">contains the old childNodes</param>
        /// <param name="newChildren">contains the new childNodes</param>
        /// <returns>if a changes was found: the AstNode where the first 
        /// change is detected, null otherwise</returns>
        private static AstNode TraverseMethodBodyIgnoringComments(
            IEnumerable<AstNode> oldChildren,
            IEnumerable<AstNode> newChildren)
        {
            int counter = 0;
            AstNode change = null;
            AstNode newChild = null;
            foreach (AstNode oldChild in oldChildren)
            {
                if (counter >= newChildren.Count())
                {
                    return newChild;
                }

                //the role of a node indicates if it is a comment or not
                //skip all comments
                if (oldChild.Role.ToString().Equals(s_CommentString))
                {
                    continue;
                }

                newChild = newChildren.ElementAt(counter);

                //skip all comments
                while (newChild.Role.ToString().Equals(s_CommentString))
                {
                    counter ++;
                    newChild = newChildren.ElementAt(counter);
                }

                //if the children also has children then the same method 
                //is called for the child.children
                //otherwise the text of the current old and new node is compared
                if (oldChild.HasChildren)
                {
                    if (!newChild.HasChildren)
                    {
                        change = newChild;
                        return change;
                    }

                    change = TraverseMethodBodyIgnoringComments
                        (oldChild.Children, newChild.Children);
                    if (change != null)
                    {
                        return change;
                    }
                }
                else
                {
                    string old = oldChild.GetText();
                    string newText = newChild.GetText();
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

        /// <summary>
        /// Compares all oldChildNodes with all newChildNodes.
        /// if a child has also any children then this method will again 
        /// be called with the childs.Children (recursion)
        /// </summary>
        /// <param name="oldChildren">contains the old childNodes</param>
        /// <param name="newChildren">contains the new childNodes</param>
        /// <returns>true if all old childNodes are equal to all new childNodes, 
        /// false otherwise</returns>
        private static AstNode TraverseMethodBody(
            IEnumerable<AstNode> oldChildren,
            IEnumerable<AstNode> newChildren)
        {
            int counter = 0;

            AstNode change = null;
            foreach (AstNode oldChild in oldChildren)
            {
                if (counter >= newChildren.Count())
                {
                    return null;
                }

                AstNode newChild = newChildren.ElementAt(counter);

                //if the children also has children then the same method 
                //is called for the child.children
                //otherwise the text of the current old and new node is compared
                if (oldChild.HasChildren)
                {
                    if (!newChild.HasChildren)
                    {
                        change = newChild;
                        return change;
                    }

                    change = TraverseMethodBody(oldChild.Children, newChild.Children);
                    if (change != null)
                    {
                        return change;
                    }
                }
                else
                {
                    string old = oldChild.GetText();
                    string newText = newChild.GetText();
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

        /// <summary>
        /// creates a method signature string without the parameters.
        /// e.g. public static void Main
        /// </summary>
        /// <param name="method">the method for which the signature should be created</param>
        /// <returns>a string of the method signature(modifers, returnValue, name)</returns>
        private static string GetMethodSignatureString(MethodDeclaration method)
        {
            string signature = String.Empty;
            signature += GetModifiersAsString(method.Modifiers) + method.ReturnType + " " +
                         method.Name;

            return signature;
        }

        /// <summary>
        /// creates a method signature string including all parameters.
        /// e.g. public static void Main
        /// </summary>
        /// <param name="method">the method for which the signature should be created</param>
        /// <returns>a string of the method signature(modifers, returnValue, name, (parameters))</returns>
        private static string GetMethodSignatureWithParameters(MethodDeclaration method)
        {
            string signature = String.Empty;
            signature += GetModifiersAsString(method.Modifiers) + method.ReturnType + " " +
                         method.Name + "(" + GetParametersAsStrings(method) + ")";

            return signature;
        }

        /// <summary>
        /// creates a string which contains all parameters of a method 
        /// seperated by a ','
        /// </summary>
        /// <param name="method">the method from which the parameter string should 
        /// be created</param>
        /// <returns>a string containing all parameters, an empty string
        /// if the method does not contain any parameters</returns>
        private static string GetParametersAsStrings(MethodDeclaration method)
        {
            IEnumerable<ParameterDeclaration> parameters = method.Parameters;
            string parameterString = String.Empty;

            foreach (ParameterDeclaration parameter in parameters)
            {
                //add all parameters to the result string
                parameterString = parameterString + parameter.GetText() + ",";
            }

            if (!parameterString.Equals(String.Empty))
            {
                // remove the ',' after the last parameter
                parameterString = parameterString.Substring(0, parameterString.Length - 1);
            }

            return parameterString;
        }

        /// <summary>
        /// creates a string containing the modifiers of a method.
        /// e.g. "public static"
        /// </summary>
        /// <param name="methodModifiers">the modifiers for which the string should be created</param>
        /// <returns>a string containing</returns>
        private static string GetModifiersAsString(Modifiers methodModifiers)
        {
            //if the method contains more than one modifier, they are seperated by ", "
            string result = methodModifiers.ToString();
            result = result.Replace(", ", " ");

            return result.ToLower();
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
            List<Error> errors = tree.Errors;

            if (errors.Count == 0)
            {
                return true;
            }
            else
            {
                foreach (Error error in errors)
                {
                    errorLogWriter.WriteLine
                        ("Error occured in File {0} in Line {1} : {2}", tree.FileName,
                            error.Region.BeginLine, error.Message);
                }
                errorLogWriter.Flush();
                return false;
            }
        }
    }
}
