using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.CSharp;

namespace Tfs_Error_Location
{
    /// <summary>
    /// Contains several properties for a methodDeclaration.
    /// </summary>
    public class Method
    {
        public Method(
            EntityDeclaration methodDecl,
            TextLocation startLocation,
            string fullName,
            string signature,
            string completeSignature)
        {
            MethodDecl = methodDecl;
            StartLocation = startLocation;
            FullyQualifiedName = fullName;
            Signature = signature;
            SignatureWithParameters = completeSignature;
        }

        /// <summary>
        /// contains the EntityDeclaration itself
        /// </summary>
        public EntityDeclaration MethodDecl
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the TextLocation where the first change in the method
        /// compared to the previous version was found.
        /// </summary>
        public Nullable<TextLocation> ChangeEntryLocation
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
        /// contains the signature of a method.
        /// modifiers returnValueType methodName.
        /// e.g public static void Main
        /// </summary>
        public string Signature
        {
            get;
            private set;
        }

        /// <summary>
        /// contains the signature with parameters of a method.
        /// modifiers returnValueType methodName (parameters).
        /// e.g public static void Main (string[] args)
        /// </summary>
        public string SignatureWithParameters
        {
            get;
            private set;
        }

        /// <summary>
        /// defines the position where the method declaration starts.
        /// all summary comments/attributes are ignored.
        /// </summary>
        public TextLocation StartLocation
        {
            get;
            private set;
        }

        /// <summary>
        /// sets the changeEntryLocation based on the startlocation in the astNode
        /// </summary>
        /// <param name="change">contains the node which was changed</param>
        public void SetChangeEntryLocation(AstNode change)
        {
            ChangeEntryLocation = change.StartLocation;
        }

        /// <summary>
        /// filters the method name from the fully qualified name
        /// </summary>
        /// <returns>the name of the method</returns>
        public string GetMethodName()
        {
            string[] nameTokens = FullyQualifiedName.Split('.');
            if (nameTokens.Length > 1)
            {
                return nameTokens[nameTokens.Length - 1];
            }

            return String.Empty;
        }

        /// <summary>
        /// clears the methodDeclaration property.
        /// this could be used in order to save memory.
        /// </summary>
        public void ClearMethodDeclaration()
        {
            MethodDecl = null;
        }


    }
}
