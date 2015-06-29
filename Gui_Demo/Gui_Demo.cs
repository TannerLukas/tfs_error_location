using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.NRefactory.CSharp;
using Tfs_Error_Location;

namespace Gui_Demo
{
    public partial class Form1 : Form
    {
        private Dictionary<AstComparer.MethodStatus, List<MethodDeclaration>>
            m_MethodComparisonResult;

        private int m_SelectedChars = 0;

        public Form1()
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

            MemoryStream stream;
            m_MethodComparisonResult = AstComparer.CompareSyntaxTrees
                (oldText, "oldExampleFile.cs", newText, "newExampleFile.cs", out stream);

            if (m_MethodComparisonResult == null)
            {
                TreeNode node = new TreeNode("Errors occured during parsing");
                trView.Nodes.Add(node);
                trView.Enabled = false;
                return;
            }

            trView.Enabled = true;
            CreateOutput(m_MethodComparisonResult);
        }

        private void CreateOutput(
            Dictionary<AstComparer.MethodStatus, List<MethodDeclaration>> methodComparisonResult)
        {
            foreach (AstComparer.MethodStatus methodStatus in methodComparisonResult.Keys)
            {
                List<TreeNode> treeNodes = new List<TreeNode>();
                foreach (MethodDeclaration method in methodComparisonResult[methodStatus])
                {
                    //TreeNode node = new TreeNode(AstComparer.GetMethodSignatureString(method));
                    TreeNode node = new TreeNode(method.Name);
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
            string newContent = txtNew.Text;
            string oldContent = txtOld.Text;

            DeSelectAllNodes();

            //in order to avoid that any text changes were also undone
            txtNew.Text = newContent;
            txtOld.Text = oldContent;

            TreeNode node = trView.SelectedNode;
            int nodeLevel = node.Level;

            AstComparer.MethodStatus methodStatus;
            if (nodeLevel == 0)
            {
                methodStatus = ParseMethodStatus(node.Text);
                foreach (MethodDeclaration method in m_MethodComparisonResult[methodStatus])
                {
                    SelectText(method, methodStatus);
                }
            }
            else if (nodeLevel == 1)
            {
                TreeNode parentNode = node.Parent;
                methodStatus = ParseMethodStatus(parentNode.Text);

                MethodDeclaration method = m_MethodComparisonResult[methodStatus].Find
                    (s => s.Name.Equals(node.Text));

                SelectText(method, methodStatus);
            }
        }

        private void DeSelectAllNodes()
        {
            //deselect all previously selected Nodes
            while (m_SelectedChars > 0)
            {
                m_SelectedChars--;
                txtNew.DeselectAll();
                txtOld.DeselectAll();
                txtNew.Select(0, 0);
                txtOld.Select(0, 0);
            }
        }

        private static AstComparer.MethodStatus ParseMethodStatus(string methodStatus)
        {
            AstComparer.MethodStatus status =
                (AstComparer.MethodStatus)Enum.Parse(typeof(AstComparer.MethodStatus), methodStatus);

            return status;
        }

        private void SelectText(
            MethodDeclaration method,
            AstComparer.MethodStatus methodStatus)
        {
            string text = AstComparer.GetMethodSignatureString(method);

            RichTextBox searchBox;

            if (methodStatus == AstComparer.MethodStatus.Deleted)
            {
                searchBox = txtOld;
            }
            else
            {
                searchBox = txtNew;
            }

            int startIndex = FindMyText(searchBox, text, 0, searchBox.Text.Length);

            // If string was found in the RichTextBox, highlight it
            if (startIndex >= 0)
            {
                // Find the end index. End Index = number of characters in textbox
                int selectionLength = text.Length;
                // Highlight the search string
                txtOld.SelectionBackColor = Color.Yellow;
                txtNew.SelectionBackColor = Color.Yellow;
                searchBox.Select(startIndex, selectionLength);
                m_SelectedChars += selectionLength;
            }
        }

        private static int FindMyText(
            RichTextBox searchBox,
            string txtToSearch,
            int searchStart,
            int searchEnd)
        {
            int indexOfSearchText = -1;

            if (searchStart >= 0)
            {
                // A valid ending index
                if (searchEnd > searchStart ||
                    searchEnd == -1)
                {
                    // Find the position of search string in RichTextBox
                    indexOfSearchText = searchBox.Find
                        (txtToSearch, searchStart, searchEnd, RichTextBoxFinds.None);
                }
            }

            return indexOfSearchText;
        }
    }
}
