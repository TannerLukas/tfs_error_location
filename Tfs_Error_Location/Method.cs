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
            List<AstNode> changeNodes,
            string fullName,
            string signature,
            string completeSignature)
        {
            MethodDecl = methodDecl;
            StartLocation = startLocation;
            ChangeNodes = changeNodes;
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
        /// contains a list of the nodes which were changed
        /// </summary>
        public List<AstNode> ChangeNodes
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
        /// all summary comments are ignored.
        /// </summary>
        public TextLocation StartLocation
        {
            get;
            private set;
        }

        /// <summary>
        /// adds a new node to the list of changedNodes
        /// </summary>
        /// <param name="change">contains the node which was changed</param>
        public void AddChangeNode(AstNode change)
        {
            if (ChangeNodes == null)
            {
                ChangeNodes = new List<AstNode>();
            }

            ChangeNodes.Add(change);
        }
    }
}
