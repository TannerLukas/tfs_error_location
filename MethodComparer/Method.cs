using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace MethodComparison
{
    /// <summary>
    /// Contains several properties for a method of type method/constructor/property.
    /// </summary>
    public class Method
    {
        private const int s_ParameterRegexValueIndex = 1;

        public Method(
            Location startLocation,
            string namespaceName,
            string typeName,
            string methodName,
            string seperator,
            string signature,
            MethodComparer.MethodType type)
        {
            StartLocation = startLocation;
            TypeName = typeName;
            MethodName = methodName;
            NamespaceName = namespaceName;
            FullyQualifiedName = CreateFullyQualifiedMethodName
                (methodName, typeName, namespaceName, seperator);
            Signature = signature;
            MethodType = type;
        }

        /// <summary>
        /// contains the name of the method
        /// </summary>
        public string MethodName
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the namespace(s) of the method.
        /// if the method is defined in a subnamespace then the namespace
        /// equals namespace.subnamespace
        /// </summary>
        public string NamespaceName
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the typeName(s) of the method.
        /// if the method is defined in a subclass then the typeName
        /// equals class.subclass
        /// </summary>
        public string TypeName
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the fully qualified name of a method.
        /// namespace.class.methodName
        /// </summary>
        public string FullyQualifiedName
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the TextLocation where the first change in the method
        /// compared to the previous version was found. if no change was
        /// found the value equals null.
        /// </summary>
        public Location ChangeEntryLocation
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the signature with parameters of a method.
        /// modifiers returnValueType methodName (parameters).
        /// e.g public static void Main (string[] args)
        /// </summary>
        public string Signature
        {
            get;
            private set;
        }

        /// <summary>
        /// defines the position where the method declaration starts.
        /// </summary>
        public Location StartLocation
        {
            get;
            private set;
        }

        /// <summary>
        /// defines the type of the method (method, property or constructor)
        /// </summary>
        public MethodComparer.MethodType MethodType
        {
            get;
            private set;
        }

        /// <summary>
        /// sets the changeEntryLocation based on the startlocation in the astNode
        /// </summary>
        /// <param name="change">contains the node which was changed</param>
        public void SetChangeEntryLocation(SyntaxNodeOrToken change)
        {
            ChangeEntryLocation = change.GetLocation();
        }

        /// <summary>
        /// retrieves the parameters from the method from its signature
        /// </summary>
        /// <returns>the parameters of a method as a string, if it does
        /// not have any parameters String.Empty is returned.</returns>
        public string GetParameters()
        {
            //matches everything inside ()
            Regex paramRegex = new Regex(@"\(([^)]*)\)");
            Match match = paramRegex.Match(Signature);

            return match.Groups[s_ParameterRegexValueIndex].Value;
        }

        /// <summary>
        /// creates the fully qualified method name
        /// </summary>
        /// <param name="methodName">contains the name of the method</param>
        /// <param name="typeName">contains the typeName of the method</param>
        /// <param name="namespaceName">contains the namespaceName of the method</param>
        /// <param name="seperator"></param>
        /// <returns>the fully qualified method name (seperated by '.')</returns>
        private static string CreateFullyQualifiedMethodName(
            string methodName,
            string typeName,
            string namespaceName,
            string seperator)
        {
            string name = typeName + seperator + methodName;
            if (!namespaceName.Equals(String.Empty))
            {
                name = namespaceName + seperator + name;
            }

            return name;
        }

        
    }
}
