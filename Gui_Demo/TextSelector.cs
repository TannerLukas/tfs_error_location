using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.CSharp;
using Tfs_Error_Location;

namespace Gui_Demo
{
    /// <summary>
    /// selects texts in a textbox which contains the code of a c# file.
    /// Three Options:
    /// 1) Selected all methods from a status
    /// 2) Select a single method
    /// 3) Select the change entry point in a method which was changed.
    /// </summary>
    internal class TextSelector
    {
        public static void SelectTextInTextBox(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox,
            MethodComparisonResult methodComparisonResult)
        {
            if (node == null ||
                methodComparisonResult == null)
            {
                return;
            }

            string newContent = newTextBox.Text;
            string oldContent = oldTextBox.Text;

            DeSelectAllTextBoxes(oldTextBox, newTextBox);

            //in order to avoid that any text changes were also undone
            newTextBox.Text = newContent;
            oldTextBox.Text = oldContent;

            int nodeLevel = node.Level;

            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> statusResult =
                methodComparisonResult.GetMethodsForStatus();

            switch (nodeLevel)
            {
                case 0:
                    SelectAllMethodsForStatus(node, oldTextBox, newTextBox, statusResult);
                    break;
                case 1:
                    SelectSingleMethod(node, oldTextBox, newTextBox, statusResult);
                    break;
                case 2:
                    SelectChangeEntryNode(node, oldTextBox, newTextBox, statusResult);
                    break;
                default:
                    break;
            }
        }

        private static void SelectAllMethodsForStatus(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox,
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> methodComparisonResult)
        {
            MethodComparisonResult.MethodStatus methodStatus = ParseMethodStatus(node.Text);
            RichTextBox searchBox = GetTextBoxToSearchIn(methodStatus, oldTextBox, newTextBox);

            if (methodComparisonResult[methodStatus].Any())
            {
                foreach (Method method in methodComparisonResult[methodStatus])
                {
                    SelectText(method.Signature, searchBox);
                }

                searchBox.ScrollToCaret();
            }
        }

        private static void SelectSingleMethod(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox,
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> methodComparisonResult)
        {
            TreeNode parentNode = node.Parent;
            MethodComparisonResult.MethodStatus methodStatus = ParseMethodStatus(parentNode.Text);

            RichTextBox searchBox = GetTextBoxToSearchIn(methodStatus, oldTextBox, newTextBox);

            string methodName = node.Text;

            Method method = methodComparisonResult[methodStatus].Find
                (s => s.FullyQualifiedName.Equals(methodName));

            SelectText(method.Signature, searchBox);
            searchBox.ScrollToCaret();
        }

        private static void SelectSingleMethod2(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox,
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> methodComparisonResult)
        {
            TreeNode parentNode = node.Parent;
            MethodComparisonResult.MethodStatus methodStatus = ParseMethodStatus(parentNode.Text);

            RichTextBox searchBox = GetTextBoxToSearchIn(methodStatus, oldTextBox, newTextBox);

            string methodName = node.Text;

            Method method = methodComparisonResult[methodStatus].Find
                (s => s.FullyQualifiedName.Equals(methodName));

            SelectText(method.Signature, searchBox);
            searchBox.ScrollToCaret();
        }


        private static void SelectChangeEntryNode(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox,
            Dictionary<MethodComparisonResult.MethodStatus, List<Method>> methodComparisonResult)
        {
            //methodstatus node
            TreeNode parentNode = node.Parent.Parent;
            MethodComparisonResult.MethodStatus methodStatus = ParseMethodStatus(parentNode.Text);
            string methodName = node.Parent.Text;

            RichTextBox searchBox = GetTextBoxToSearchIn(methodStatus, oldTextBox, newTextBox);

            string[] lines = searchBox.Text.Split('\n');

            Method method = methodComparisonResult[methodStatus].Find
                (s => s.FullyQualifiedName.Equals(methodName));

            if (method != null &&
                method.ChangeNodes.Count > 0)
            {
                AstNode changedNode = method.ChangeNodes.First();
                if (changedNode != null)
                {
                    TextLocation startLocation = changedNode.StartLocation;
                    TextLocation endLocation = changedNode.EndLocation;
                    int startLine = startLocation.Line;
                    int startColumn = startLocation.Column;
                    //int endLine = endLocation.Line;
                    int endColumn = endLocation.Column;

                    string textSoFar = "";
                    for (int i = 0; i < startLine - 1; i++)
                    {
                        textSoFar += lines[i] + "\n";
                    }
                    int startIndex = textSoFar.Length + startColumn - 1;
                    int selectionLength = endColumn - startColumn;

                    SelectChangeEntryPoint(startIndex, selectionLength, searchBox);
                }
            }
        }

        private static RichTextBox GetTextBoxToSearchIn(
            MethodComparisonResult.MethodStatus status,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            RichTextBox box;
            if (status == MethodComparisonResult.MethodStatus.Deleted)
            {
                box = oldTextBox;
            }
            else
            {
                box = newTextBox;
            }
            return box;
        }


        private static MethodComparisonResult.MethodStatus ParseMethodStatus(string methodStatus)
        {
            MethodComparisonResult.MethodStatus status =
                (MethodComparisonResult.MethodStatus)
                    Enum.Parse(typeof(MethodComparisonResult.MethodStatus), methodStatus);

            return status;
        }

        private static void SelectText(
            string searchText,
            RichTextBox searchBox)
        {
            int startIndex = FindMyText(searchBox, searchText, 0, searchBox.Text.Length);

            // If string was found in the RichTextBox, highlight it
            if (startIndex >= 0)
            {
                // Find the end index. End Index = number of characters in textbox
                int selectionLength = searchText.Length;
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

        private static void SelectChangeEntryPoint(
            int startIndex,
            int selectionLength,
            RichTextBox searchBox)
        {
            //Select the text
            searchBox.Select(startIndex, selectionLength);
            searchBox.HideSelection = true;
            searchBox.SelectionBackColor = Color.Yellow;
            searchBox.ScrollToCaret();
            searchBox.DeselectAll();
        }

        public static void DeSelectAllTextBoxes(
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            newTextBox.DeselectAll();
            newTextBox.Refresh();
            oldTextBox.DeselectAll();
            oldTextBox.Refresh();
        }
    }
}
