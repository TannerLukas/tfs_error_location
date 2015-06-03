using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
using Mono.CSharp;
using Modifiers = ICSharpCode.NRefactory.CSharp.Modifiers;
using Statement = ICSharpCode.NRefactory.CSharp.Statement;

namespace Tfs_Error_Location
{
    internal class AstComparer
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

        public static Dictionary<MethodStatus, List<MethodDeclaration>> CompareSyntaxTrees(
            SyntaxTree oldTree,
            SyntaxTree newTree)
        {
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

            Dictionary<MethodStatus, List<MethodDeclaration>> result = CreateMethodComparisonResult
                (methodMapping, addedMethods, deletedMethods);

            return result;
        }

        private static Dictionary<MethodStatus, List<MethodDeclaration>> CreateMethodComparisonResult(
            Dictionary<MethodDeclaration, MethodDeclaration> methodMapping,
            List<MethodDeclaration> addedMethods,
            List<MethodDeclaration> deletedMethods)
        {
            Dictionary<MethodStatus, List<MethodDeclaration>> result = CompareMatchedMethods
                (methodMapping);

            //add the added and deleted Method with their corresponding status to the result
            result[MethodStatus.Added] = addedMethods;
            result[MethodStatus.Deleted] = deletedMethods;

            return result;
        }

        private static Dictionary<MethodStatus, List<MethodDeclaration>> CompareMatchedMethods(
            Dictionary<MethodDeclaration, MethodDeclaration> methodMapping)
        {
            //init result: create an entry for each possible MethodStatus
            Dictionary<MethodStatus, List<MethodDeclaration>> result =
                new Dictionary<MethodStatus, List<MethodDeclaration>>
                {
                    {MethodStatus.NotChanged, new List<MethodDeclaration>()},
                    {MethodStatus.Changed, new List<MethodDeclaration>()},
                    {MethodStatus.Added, new List<MethodDeclaration>()},
                    {MethodStatus.Deleted, new List<MethodDeclaration>()}
                };

            foreach (KeyValuePair<MethodDeclaration, MethodDeclaration> pair in methodMapping)
            {
                MethodDeclaration oldMethod = pair.Key;
                MethodDeclaration newMethod = pair.Value;

                //1. Compare Modifiers
                //2. Compare ReturnValueType
                //(3. Compare Name)
                //4. Compare Parameters
                //5. Compare MethodBody

                bool hasNoChanges = CompareMethodModifiers(oldMethod, newMethod) &&
                                    CompareReturnValueType(oldMethod, newMethod) &&
                                    CompareParameters(oldMethod, newMethod) &&
                                    CompareMethodBody(oldMethod, newMethod);

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

        private static bool CompareMethodBody(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            bool result = true;

            BlockStatement oldBody = oldMethod.Body;
            AstNodeCollection<Statement> oldStatements = oldBody.Statements;

            BlockStatement newBody = newMethod.Body;
            AstNodeCollection<Statement> newStatements = newBody.Statements;

            foreach (Statement oldStatement in oldStatements)
            {
                //TraverseStatements(oldStatement.Children);
                result = result && TraverseStatements(oldStatements, newStatements);

                if (!result)
                {
                    return result;
                }
            }

            return result;
        }

        private static bool TraverseStatements(
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

                if (oldChild.HasChildren)
                {
                    if (!newChild.HasChildren)
                    {
                        return false;
                    }

                    result = result && TraverseStatements(oldChild.Children, newChild.Children);
                }
                else
                {
                    string old = oldChild.GetText();
                    string newText = newChild.GetText();

                    return (old.Equals(newText));
                }

                counter++;
            }

            return result;
        }

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
                string oldParameterName;
                ParameterModifier oldParameterModifier;
                string oldParameterType;

                GetParameterMember
                    (parameter, out oldParameterName, out oldParameterType, out oldParameterModifier);

                ParameterDeclaration newParameter = newParameters.ElementAt(counter);
                string newParameterName;
                ParameterModifier newParameterModifier;
                string newParameterType;

                GetParameterMember
                    (newParameter, out newParameterName, out newParameterType,
                        out newParameterModifier);

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

        private static void GetParameterMember(
            ParameterDeclaration parameter,
            out string parameterName,
            out string parameterType,
            out ParameterModifier parameterModifier)
        {
            parameterName = parameter.Name;
            parameterModifier = parameter.ParameterModifier;
            parameterType = parameter.Type.ToString();
        }

        private static bool CompareReturnValueType(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            AstType oldReturnType = oldMethod.ReturnType;
            AstType newReturnType = newMethod.ReturnType;

            //var a = oldReturnType.NodeType;

            string oldType = oldReturnType.ToString();
            string newType = newReturnType.ToString();

            return (oldType.Equals(newType));
        }

        private static bool CompareMethodModifiers(
            MethodDeclaration oldMethod,
            MethodDeclaration newMethod)
        {
            Modifiers oldModifiers = oldMethod.Modifiers;
            Modifiers newModifiers = newMethod.Modifiers;

            return (oldModifiers == newModifiers);
        }

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

        private static List<MethodDeclaration> FindAllNewMethodDeclarations(
            IEnumerable<MethodDeclaration> mappedMethods,
            IEnumerable<MethodDeclaration> newMethods)
        {
            List<MethodDeclaration> addedMethods = new List<MethodDeclaration>();

            foreach (MethodDeclaration newMethod in newMethods)
            {
                //a not existing mapping indicates that the method was added 
                if (!mappedMethods.Contains(newMethod))
                {
                    addedMethods.Add(newMethod);
                }
            }

            return addedMethods;
        }

        private static IEnumerable<MethodDeclaration> GetAllMethodDeclarations(SyntaxTree tree)
        {
            IEnumerable<MethodDeclaration> methods = tree.Descendants.OfType<MethodDeclaration>();
            return methods;
        }

        private static MethodDeclaration FindMatchingMethodDeclaration(
            MethodDeclaration oldMethod,
            IEnumerable<MethodDeclaration> newMethods)
        {
            string methodName = oldMethod.Name;

            //null is returned if no matching method was found
            foreach (MethodDeclaration newMethod in newMethods)
            {
                //do specific comparisons
                //for now only the name

                if (methodName == newMethod.Name)
                {
                    return newMethod;
                }
            }

            return null;
        }
    }
}
