using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ICSharpCode.NRefactory.CSharp;
using Tfs_Error_Location;

namespace Gui_Demo
{
    public partial class GuiDemo : Form
    {
        public GuiDemo()
        {
            InitializeComponent();
        }

        private void OnCompareClick(
            object sender,
            EventArgs e)
        {
            string oldText = txtOld.Text;
            string newText = txtNew.Text;

            trView.Nodes.Clear();
            TextSelector.DeSelectTextBox(txtOld);
            TextSelector.DeSelectTextBox(txtNew);

            MethodComparisonResult methodComparisonResult;
            using (MemoryStream errorLogStream = new MemoryStream())
            {
                methodComparisonResult = AstComparer.CompareSyntaxTrees
                    (oldText, "oldExampleFile.cs", newText, "newExampleFile.cs", errorLogStream);

                if (methodComparisonResult == null)
                {
                    //for now the treeView is disabled if an error occurs in the AstComparer
                    TreeNode node = new TreeNode("Errors occured during parsing");
                    trView.Nodes.Add(node);
                    trView.Enabled = false;
                    return;
                }
            }



            trView.Enabled = true;
            CreateTreeViewOutput(methodComparisonResult);

            txtOld.ScrollToCaret();
            txtNew.ScrollToCaret();
        }

        private void CreateTreeViewOutput(MethodComparisonResult methodComparisonResult)
        {
            //for each method status: get the list of methods which belong to that status
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> statusResult =
                methodComparisonResult.GetMethodsForStatus();

            //for each status add a treeNode, for each method of that status 
            //add a childTreeNode
            foreach (MethodComparisonResult.MethodStatus methodStatus in statusResult.Keys)
            {
                List<TreeNode> treeNodes = new List<TreeNode>();
                foreach (Method method in statusResult[methodStatus])
                {
                    List<TreeNode> changedNodes = new List<TreeNode>();

                    foreach (AstNode changeNode in method.ChangeNodes)
                    {
                        changedNodes.Add(new TreeNode("Change Entry Point") {Tag = changeNode});
                    }

                    TreeNode node = new TreeNode(method.FullyQualifiedName, changedNodes.ToArray())
                    {
                        Tag = method
                    };

                    treeNodes.Add(node);
                }

                TreeNode methodStatusNode = new TreeNode
                    (methodStatus.ToString(), treeNodes.ToArray())
                {
                    Tag = statusResult[methodStatus]
                };

                trView.Nodes.Add(methodStatusNode);
            }
        }

        private void OnNodeDoubleClick(
            object sender,
            TreeNodeMouseClickEventArgs e)
        {
            TextSelector.SelectTextInTextBox(trView, txtOld, txtNew);
        }
    }
}
