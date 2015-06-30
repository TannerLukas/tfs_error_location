using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Mono.CSharp;
using CSharpParser = ICSharpCode.NRefactory.CSharp.CSharpParser;
using Modifiers = ICSharpCode.NRefactory.CSharp.Modifiers;
using ParameterModifier = ICSharpCode.NRefactory.CSharp.ParameterModifier;

namespace Tfs_Error_Location
{
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
        /// <param name="errorLogStream">contains the stream with all errors</param>
        /// <param name="ignoreComments">A flag which indicates if changes to comments 
        /// should also be taken into account when changes of a method are calculated.
        /// If it is true, all comments are ignored.</param>
        /// <returns>on success: a dictionary containing the possible states of methods as keys,
        /// and a list of their corresponding methods as values, null otherwise</returns>
        internal static Dictionary<MethodStatus, List<MethodDeclaration>> CompareSyntaxTrees(
            string oldFileContent,
            string oldFileName,
            string newFileContent,
            string newFileName,
            out MemoryStream errorLogStream,
            bool ignoreComments = true)
        {
            //retrieve the syntaxTrees for the old and the new file
            SyntaxTree oldTree = GetSyntaxTree(oldFileContent, oldFileName);
            SyntaxTree newTree = GetSyntaxTree(newFileContent, newFileName);

            errorLogStream = new MemoryStream();
            StreamWriter errorLogWriter = new StreamWriter(errorLogStream);

            if (CheckSyntaxTreeErrors(oldTree, errorLogWriter) == false ||
                CheckSyntaxTreeErrors(newTree, errorLogWriter) == false)
            {
                return null;
            }

            //get all methods for both files
            IEnumerable<MethodDeclaration> oldMethods = GetAllMethodDeclarations(oldTree);
            IEnumerable<MethodDeclaration> newMethods = GetAllMethodDeclarations(newTree);

            //create a methodMapping
            //this is needed because we need to know which methods should be compared
            //a new location for a method in the code file is ignored
            //renaming is not handled yet
            List<MethodDeclaration> addedMethods;
            List<MethodDeclaration> deletedMethods;
            Dictionary<MethodDeclaration, MethodDeclaration> methodMapping = CreateMethodMapping
                (oldMethods, newMethods, out addedMethods, out deletedMethods);

            Dictionary<MethodStatus, List<MethodDeclaration>> result = CompareMatchedMethods
                (methodMapping, ignoreComments);

            //add the added and deleted Method with their corresponding status to result
            result[MethodStatus.Added] = addedMethods;
            result[MethodStatus.Deleted] = deletedMethods;

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
                        ("Error occured in File {0} : {1}", tree.FileName, error.Message);
                }
                errorLogWriter.Flush();
                return false;
            }
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
        /// retrieves all methodDeclarations from the given SyntaxTree
        /// </summary>
        /// <param name="tree">the tree from which the methods should be retrieved</param>
        /// <returns>a list of all methodDeclarations in a SyntaxTree</returns>
        private static IEnumerable<MethodDeclaration> GetAllMethodDeclarations(SyntaxTree tree)
        {
            IEnumerable<MethodDeclaration> methods = tree.Descendants.OfType<MethodDeclaration>();
            return methods;
        }

        /// <summary>
        /// creates the fully classified methodName of a method.
        /// (namespace.type1.type2.methodName)
        /// </summary>
        /// <param name="method">the method for which the name should be returned</param>
        /// <returns>the fully classified methodName</returns>
        public static string GetFullyQualifiedMethodName(MethodDeclaration method)
        {
            TypeDeclaration classType = method.GetParent<TypeDeclaration>();
            AstNode nextParent = classType.Parent;

            string fullClassName = GetClassName(classType) + "." + method.Name;

            //a class could be defined within a class
            //therefore, each class name is added to the fully qualified name
            //finally the namespace is added
            while (nextParent != null)
            {
                if (nextParent.GetType() == typeof(TypeDeclaration))
                {
                    fullClassName = GetClassName((TypeDeclaration)nextParent) + "." + fullClassName;
                }
                else
                {
                    //reached the namespace declaration
                    NamespaceDeclaration namespaceDeclaration =
                        method.GetParent<NamespaceDeclaration>();

                    if (namespaceDeclaration != null)
                    {
                        fullClassName = namespaceDeclaration.Name + "." + fullClassName;
                    }

                    return fullClassName;
                }

                nextParent = nextParent.Parent;
            }

            return fullClassName;
        }

        /// <summary>
        /// gets the name attribute for the given type and returns it string value (=className)
        /// </summary>
        /// <param name="type">the type from which the className should be retrieved</param>
        /// <returns>the value of the name attribute of a given 
        /// typedeclaration (= className)</returns>
        private static string GetClassName(TypeDeclaration type)
        {
            var nameProperty =
                type.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(s => s.Name.Equals("Name"));

            object nameValue = nameProperty.First().GetValue(type, null);
            string className = nameValue.ToString();

            return className;
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
        private static Dictionary<MethodDeclaration, MethodDeclaration> CreateMethodMapping(
            IEnumerable<MethodDeclaration> oldMethods,
            IEnumerable<MethodDeclaration> newMethods,
            out List<MethodDeclaration> addedMethods,
            out List<MethodDeclaration> deletedMethods)
        {
            Dictionary<MethodDeclaration, MethodDeclaration> methodMapping =
                new Dictionary<MethodDeclaration, MethodDeclaration>();

            deletedMethods = new List<MethodDeclaration>();

            foreach (MethodDeclaration oldMethod in oldMethods)
            {
                //try to find the old method declaration in the new file
                MethodDeclaration matchingMethod = FindMatchingMethodDeclaration
                    (oldMethod, newMethods);

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
            addedMethods = FindAllNewMethodDeclarations(methodMapping.Values, newMethods);

            return methodMapping;
        }

        /// <summary>
        /// try to find a methodDeclaration in the newMethodDeclarations.
        /// the comparison is only done by comparing the names.
        /// </summary>
        /// <param name="oldMethod">the method to which a matching method should be found</param>
        /// <param name="newMethods">the list of all MethodDeclarations of the new file</param>
        /// <returns>on success: the matchingMethod, null otherwise</returns>
        private static MethodDeclaration FindMatchingMethodDeclaration(
            MethodDeclaration oldMethod,
            IEnumerable<MethodDeclaration> newMethods)
        {
            string methodName = GetFullyQualifiedMethodName(oldMethod);

            //null is returned if no matching method was found
            foreach (MethodDeclaration newMethod in newMethods)
            {
                //do specific comparisons
                //for now only the name

                if (methodName == GetFullyQualifiedMethodName(newMethod))
                {
                    return newMethod;
                }
            }

            return null;
        }

        /// <summary>
        /// checks for which methods no mapping exists, because this indicates that they
        /// were added
        /// </summary>
        /// <param name="mappedMethods">the list of methods where a mapping was already found</param>
        /// <param name="newMethods">the list of all MethodDeclarations of the new file</param>
        /// <returns>a list of methods which were added in the new file</returns>
        private static List<MethodDeclaration> FindAllNewMethodDeclarations(
            IEnumerable<MethodDeclaration> mappedMethods,
            IEnumerable<MethodDeclaration> newMethods)
        {
            List<MethodDeclaration> addedMethods = new List<MethodDeclaration>();

            foreach (MethodDeclaration newMethod in newMethods)
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
        /// 1) compare Modifiers
        /// 2) compare ReturnValueType
        /// 3) compare Parameters
        /// 4) Compare MethodBody
        /// </summary>
        /// <param name="methodMapping">a mapping of the matching methods</param>
        /// <param name="ignoreComments">indicates if comments should be taken into
        /// account while calculating the methodcomparison result</param>
        /// <returns>a dictionary containing the methods which were changed and the 
        /// methods which were not changed.</returns>
        private static Dictionary<MethodStatus, List<MethodDeclaration>> CompareMatchedMethods(
            Dictionary<MethodDeclaration, MethodDeclaration> methodMapping,
            bool ignoreComments)
        {
            Dictionary<MethodStatus, List<MethodDeclaration>> result =
                new Dictionary<MethodStatus, List<MethodDeclaration>>
                {
                    {MethodStatus.NotChanged, new List<MethodDeclaration>()},
                    {MethodStatus.Changed, new List<MethodDeclaration>()}
                };

            foreach (KeyValuePair<MethodDeclaration, MethodDeclaration> pair in methodMapping)
            {
                MethodDeclaration oldMethod = pair.Key;
                MethodDeclaration newMethod = pair.Value;

                bool hasNoChanges = CompareMethodModifiers(oldMethod, newMethod) &&
                                    CompareReturnValueType(oldMethod, newMethod) &&
                                    CompareParameters(oldMethod, newMethod) &&
                                    CompareMethodBody(oldMethod, newMethod, ignoreComments);

                if (hasNoChanges)
                {
                    result[MethodStatus.NotChanged].Add(newMethod);
                }
                else
                {
                    result[MethodStatus.Changed].Add(newMethod);
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
        /// 1) retrieves all childnodes from the methodbodies
        /// 2) traverses through their children and compares them
        /// </summary>
        /// <param name="oldMethod">contains the old MethodDeclaration</param>
        /// <param name="newMethod">contains the new MethodDeclaration</param>
        /// <param name="ignoreComments">indicates if comments should be taken into
        /// account while calculating the methodcomparison result</param>
        /// <returns>true if the methods have the same body, false otherwise</returns>
        private static bool CompareMethodBody(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod,
            bool ignoreComments)
        {
            bool result = true;

            BlockStatement oldBody = oldMethod.Body;
            IEnumerable<AstNode> oldChilds = oldBody.Children;

            BlockStatement newBody = newMethod.Body;
            IEnumerable<AstNode> newChilds = newBody.Children;

            //Comments are ignored if s_IgnoreComments == true
            //otherwise, comment changes indicate that the method has changed.
            if (ignoreComments)
            {
                result = TraverseMethodBodyIgnoringComments(oldChilds, newChilds);
            }
            else
            {
                result = TraverseMethodBody(oldChilds, newChilds);
            }


            return result;
        }

        /// <summary>
        /// Compares all oldChildNodes with all newChildNodes.
        /// if a child has also any children then this method will again 
        /// be called with the childs.Children (recursion). All comments 
        /// are ignored.
        /// </summary>
        /// <param name="oldChildren">contains the old childNodes</param>
        /// <param name="newChildren">contains the new childNodes</param>
        /// <returns>true if all old childNodes are equal to all new childNodes, 
        /// false otherwise</returns>
        private static bool TraverseMethodBodyIgnoringComments(
            IEnumerable<AstNode> oldChildren,
            IEnumerable<AstNode> newChildren)
        {
            int counter = 0;
            bool result = true;
            foreach (AstNode oldChild in oldChildren)
            {
                if (counter >= newChildren.Count())
                {
                    return false;
                }

                //the role of a node indicates if it is a comment or not
                //skip all comments
                if (oldChild.Role.ToString().Equals(s_CommentString))
                {
                    continue;
                }

                AstNode newChild = newChildren.ElementAt(counter);

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
                        return false;
                    }

                    result = result &&
                             TraverseMethodBodyIgnoringComments
                                 (oldChild.Children, newChild.Children);
                }
                else
                {
                    string old = oldChild.GetText();
                    string newText = newChild.GetText();
                    if (!old.Equals(newText))
                    {
                        return false;
                    }
                }

                counter++;
            }

            return result;
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
        private static bool TraverseMethodBody(
            IEnumerable<AstNode> oldChildren,
            IEnumerable<AstNode> newChildren)
        {
            int counter = 0;
            bool result = true;
            foreach (AstNode oldChild in oldChildren)
            {
                if (counter >= newChildren.Count())
                {
                    return false;
                }

                AstNode newChild = newChildren.ElementAt(counter);

                //if the children also has children then the same method 
                //is called for the child.children
                //otherwise the text of the current old and new node is compared
                if (oldChild.HasChildren)
                {
                    if (!newChild.HasChildren)
                    {
                        return false;
                    }

                    result = result && TraverseMethodBody(oldChild.Children, newChild.Children);
                }
                else
                {
                    string old = oldChild.GetText();
                    string newText = newChild.GetText();
                    if (!old.Equals(newText))
                    {
                        return false;
                    }
                }

                counter++;
            }

            return result;
        }

        /// <summary>
        /// creates a method signature string.
        /// e.g. public static void Main
        /// </summary>
        /// <param name="method">the method for which the signature should be created</param>
        /// <returns>a string of the method signature(modifers, returnValue, name, [parameters])</returns>
        internal static string GetMethodSignatureString(MethodDeclaration method)
        {
            string signature = String.Empty;
            signature += GetModifiersAsString(method.Modifiers) + method.ReturnType + " " +
                         method.Name;

            return signature;
        }

        /// <summary>
        /// creates a string containing the modifiers of a method.
        /// e.g. "public static"
        /// </summary>
        /// <param name="methodModifiers">the modifiers for which the string should be created</param>
        /// <returns>a string containing</returns>
        private static string GetModifiersAsString(Modifiers methodModifiers)
        {
            string result = String.Empty;
            if (methodModifiers.ToString().Contains(","))
            {
                string[] mods = methodModifiers.ToString().Split(',');
                foreach (string mod in mods)
                {
                    result += mod.Trim().ToLower() + " ";
                }
            }
            else
            {
                result = methodModifiers.ToString().ToLower() + " ";
            }

            return result;
        }
    }
}
