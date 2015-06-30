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
                    TreeNode node = new TreeNode(AstComparer.GetFullyQualifiedMethodName(method));
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

            DeSelectAllTextBoxes();

            //in order to avoid that any text changes were also undone
            txtNew.Text = newContent;
            txtOld.Text = oldContent;

            TreeNode node = trView.SelectedNode;

            if (node == null)
            {
                return;
            }
            int nodeLevel = node.Level;

            AstComparer.MethodStatus methodStatus;
            RichTextBox searchBox;
            if (nodeLevel == 0)
            {
                methodStatus = ParseMethodStatus(node.Text);
                searchBox = GetTextBoxToSearchIn(methodStatus);

                foreach (MethodDeclaration method in m_MethodComparisonResult[methodStatus])
                {
                    SelectText(method, searchBox);
                }
                searchBox.ScrollToCaret();
            }
            else if (nodeLevel == 1)
            {
                TreeNode parentNode = node.Parent;
                methodStatus = ParseMethodStatus(parentNode.Text);

                searchBox = GetTextBoxToSearchIn(methodStatus);

                string[] methodNameAttributes = node.Text.Split('.');
                string methodName = methodNameAttributes[methodNameAttributes.Count() - 1];

                MethodDeclaration method = m_MethodComparisonResult[methodStatus].Find
                    (s => s.Name.Equals(methodName));

                SelectText(method, searchBox);
                searchBox.ScrollToCaret();
            }
        }

        private RichTextBox GetTextBoxToSearchIn(AstComparer.MethodStatus status)
        {
            RichTextBox box;
            if (status == AstComparer.MethodStatus.Deleted)
            {
                box = txtOld;
            }
            else
            {
                box = txtNew;
            }
            return box;
        }

        private void DeSelectAllTextBoxes()
        {
            txtNew.DeselectAll();
            txtOld.DeselectAll();
        }

        private static AstComparer.MethodStatus ParseMethodStatus(string methodStatus)
        {
            AstComparer.MethodStatus status =
                (AstComparer.MethodStatus)Enum.Parse(typeof(AstComparer.MethodStatus), methodStatus);

            return status;
        }

        private static void SelectText(
            MethodDeclaration method,
            RichTextBox searchBox)
        {
            string text = AstComparer.GetMethodSignatureString(method);

            int startIndex = FindMyText(searchBox, text, 0, searchBox.Text.Length);

            // If string was found in the RichTextBox, highlight it
            if (startIndex >= 0)
            {
                // Find the end index. End Index = number of characters in textbox
                int selectionLength = text.Length;
                // Highlight the search string
                searchBox.SelectionBackColor = Color.Yellow;
                searchBox.Select(startIndex, selectionLength);
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
