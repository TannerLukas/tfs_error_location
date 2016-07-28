﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MethodComparison;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Gui_Demo
{
    /// <summary>
    /// Selects text in a textbox which contains the code of a C# file.
    /// Three Options:
    /// 1) Select all methods from a status
    /// 2) Select a single method
    /// 3) Select the change entry point in a method which was changed.
    /// </summary>
    internal class TextSelector
    {
        /// <summary>
        /// Selects text in a textBox. The textbox in which the selection should 
        /// be made depends on the selected node in the treeView.
        /// </summary>
        /// <param name="trView">the treeView from which the selection is triggered</param>
        /// <param name="oldTextBox">contains the content of the old file</param>
        /// <param name="newTextBox">contains the content of the new file</param>
        public static void SelectTextInTextBox(
            TreeView trView,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            DeSelectTextBox(oldTextBox);
            DeSelectTextBox(newTextBox);

            TreeNode node = trView.SelectedNode;

            if (node == null)
            {
                return;
            }

            int nodeLevel = node.Level;

            //nodelevel 0 = methodstatus
            //nodelevel 1 = single method
            //nodelevel 2 = change entry point node
            try
            {
                switch (nodeLevel)
                {
                    case 0:
                        SelectAllMethodsForStatus(node, oldTextBox, newTextBox);
                        break;
                    case 1:
                        SelectSingleMethod(node, oldTextBox, newTextBox);
                        break;
                    case 2:
                        SelectChangeEntryNode(node, oldTextBox, newTextBox);
                        break;
                }
            }
            catch (ArgumentException exception)
            {
                trView.Nodes.Clear();
                TreeNode errorNode = new TreeNode
                    (string.Format("An Error Occured: {0}", exception.Message));
                trView.Nodes.Add(errorNode);
            }
        }

        /// <summary>
        /// deselects the text in a textbox
        /// </summary>
        /// <param name="textBox">the textBox from which to all lines should be deselected</param>
        public static void DeSelectTextBox(
            RichTextBox textBox)
        {
            textBox.SelectionBackColor = Color.White;
            textBox.SelectAll();
        }

        /// <summary>
        /// selects all methods for a given method status in a textBox. the textbox 
        /// in which the selection should be made depends on the method status.
        /// </summary>
        /// <param name="node">the selected treenode</param>
        /// <param name="oldTextBox">contains the content of the old file</param>
        /// <param name="newTextBox">contains the content of the new file</param>
        private static void SelectAllMethodsForStatus(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            //get the textbox in which something should be highlighted
            RichTextBox searchBox = GetTextBoxToSearchIn(node, oldTextBox, newTextBox);

            //just to be sure everything is deselected
            DeSelectTextBox(searchBox);

            List<Method> methods = (List<Method>)node.Tag;

            foreach (Method method in methods)
            {
                SelectSyntaxNode(searchBox, method.StartLocation);
            }

            searchBox.ScrollToCaret();
        }

        /// <summary>
        /// selects a single method in a textBox. the textbox in which the selection
        /// should be made depends on the corresponding method status.
        /// </summary>
        /// <param name="node">the selected treenode</param>
        /// <param name="oldTextBox">contains the content of the old file</param>
        /// <param name="newTextBox">contains the content of the new file</param>
        private static void SelectSingleMethod(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            //get the textbox in which something should be highlighted
            TreeNode statusNode = node.Parent;
            RichTextBox searchBox = GetTextBoxToSearchIn(statusNode, oldTextBox, newTextBox);

            //just to be sure everything is deselected
            DeSelectTextBox(searchBox);

            Method method = (Method)node.Tag;

            SelectSyntaxNode(searchBox, method.StartLocation);
            searchBox.ScrollToCaret();
        }

        /// <summary>
        /// selects the change entry point of a method which was changed
        /// </summary>
        /// <param name="node">the selected treenode</param>
        /// <param name="oldTextBox">contains the content of the old file</param>
        /// <param name="newTextBox">contains the content of the new file</param>
        private static void SelectChangeEntryNode(
            TreeNode node,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            //get the textbox in which something should be highlighted
            TreeNode statusNode = node.Parent.Parent;
            RichTextBox searchBox = GetTextBoxToSearchIn(statusNode, oldTextBox, newTextBox);

            //just to be sure everything is deselected
            DeSelectTextBox(searchBox);

            Location changeLocation = (Location)node.Tag;

            SelectSyntaxNode(searchBox, changeLocation);
            searchBox.ScrollToCaret();
        }

        /// <summary>
        /// Selects the given SyntaxNode in the searchBox.
        /// </summary>
        /// <param name="searchBox">the textbox in which something should be highlighted</param>
        /// <param name="startLocation">defines where the highlighting should start</param>
        private static void SelectSyntaxNode(
            RichTextBox searchBox,
            Location startLocation)
        {
            TextSpan span = startLocation.SourceSpan;
            SelectText(searchBox, span.Start, span.Length);
        }

        /// <summary>
        /// selects text in a textBox from startIndex to startIndex+selectionLength
        /// </summary>
        /// <param name="searchBox">the textBox in which something should be hightlighted</param>
        /// <param name="startIndex">the startIndex for the selection</param>
        /// <param name="selectionLength">the length of the selection</param>
        private static void SelectText(
            RichTextBox searchBox,
            int startIndex,
            int selectionLength)
        {
            searchBox.Select(startIndex, selectionLength);
            searchBox.HideSelection = true;
            searchBox.SelectionBackColor = Color.Yellow;
        }

        /// <summary>
        /// based on the method status the text selector decides in which textbox
        /// the text should be highlighted. a deleted method can only be highlighted
        /// in the oldTextBox, the other ones are highlighted in the newTextBox.
        /// </summary>
        /// <param name="statusNode">contains the node which refers to the method status</param>
        /// <param name="oldTextBox">contains the content of the old file</param>
        /// <param name="newTextBox">contains the content of the old file</param>
        /// <returns>the TextBox in which something should be highlighted</returns>
        private static RichTextBox GetTextBoxToSearchIn(
            TreeNode statusNode,
            RichTextBox oldTextBox,
            RichTextBox newTextBox)
        {
            MethodComparisonResult.MethodStatus status =
                (MethodComparisonResult.MethodStatus)
                    Enum.Parse(typeof(MethodComparisonResult.MethodStatus), statusNode.Text);

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
    }
}
