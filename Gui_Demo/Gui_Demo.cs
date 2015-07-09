using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.CSharp;
using Tfs_Error_Location;

namespace Gui_Demo
{
    public partial class GuiDemo : Form
    {
        private MethodComparisonResult m_MethodComparisonResult;

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

            MemoryStream errorLogStream = new MemoryStream();

            m_MethodComparisonResult = AstComparer.CompareSyntaxTrees
                (oldText, "oldExampleFile.cs", newText, "newExampleFile.cs", errorLogStream);

            if (m_MethodComparisonResult == null)
            {
                TreeNode node = new TreeNode("Errors occured during parsing");
                trView.Nodes.Add(node);
                TextSelector.DeSelectAllTextBoxes(txtOld, txtNew);
                txtOld.Text = oldText;
                txtNew.Text = txtNew.Text;
                txtNew.SelectionLength = 0;
                txtNew.SelectionStart = 0;
                txtNew.Select();
                trView.Enabled = false;
                return;
            }

            trView.Enabled = true;
            CreateOutput(m_MethodComparisonResult);
        }

        private void CreateOutput(
            MethodComparisonResult methodComparisonResult)
        {
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> statusResult =
                methodComparisonResult.GetMethodsForStatus();

            foreach (MethodComparisonResult.MethodStatus methodStatus in statusResult.Keys)
            {
                List<TreeNode> treeNodes = new List<TreeNode>();
                foreach (Method method in statusResult[methodStatus])
                {
                    List<TreeNode> changedNodes = new List<TreeNode>();

                    foreach (AstNode changeNode in method.ChangeNodes)
                    {
                        changedNodes.Add(new TreeNode("Change Entry Point"));
                    }

                    TreeNode node = new TreeNode(method.FullyQualifiedName, changedNodes.ToArray());
                    treeNodes.Add(node);
                }
                TreeNode methodStatusNode = new TreeNode
                    (methodStatus.ToString(), treeNodes.ToArray());
                trView.Nodes.Add(methodStatusNode);
            }
        }

        private void OnNodeClick(
            object sender,
            TreeNodeMouseClickEventArgs e)
        {
            TreeNode node = trView.SelectedNode;
            if (node != null)
            {
                TextSelector.SelectTextInTextBox(node, txtOld, txtNew, m_MethodComparisonResult);
            }
        }

    }
}
