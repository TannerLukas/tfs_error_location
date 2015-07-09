using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.NRefactory.CSharp;

namespace Tfs_Error_Location
{
    /// <summary>
    /// Contains several properties for a methodDeclaration.
    /// </summary>
    public class Method
    {

        public Method(
            MethodDeclaration methodDecl,
            List<AstNode> changeNodes,
            string fullName,
            string signature,
            string completeSignature)
        {
            MethodDecl = methodDecl;
            ChangeNodes = changeNodes;
            FullyQualifiedName = fullName;
            Signature = signature;
            SignatureWithParameters = completeSignature;
        }

        /// <summary>
        /// contains the MethodDeclaration itself
        /// </summary>
        public MethodDeclaration MethodDecl
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
