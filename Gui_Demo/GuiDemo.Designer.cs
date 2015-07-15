namespace Gui_Demo
{
    partial class GuiDemo
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GuiDemo));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.txtOld = new System.Windows.Forms.RichTextBox();
            this.txtNew = new System.Windows.Forms.RichTextBox();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.btnCompare = new System.Windows.Forms.Button();
            this.trView = new System.Windows.Forms.TreeView();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer3);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(1012, 601);
            this.splitContainer1.SplitterDistance = 404;
            this.splitContainer1.TabIndex = 0;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 0);
            this.splitContainer3.Margin = new System.Windows.Forms.Padding(20);
            this.splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.txtOld);
            this.splitContainer3.Panel1.Padding = new System.Windows.Forms.Padding(15);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.txtNew);
            this.splitContainer3.Panel2.Padding = new System.Windows.Forms.Padding(15);
            this.splitContainer3.Size = new System.Drawing.Size(1012, 404);
            this.splitContainer3.SplitterDistance = 503;
            this.splitContainer3.TabIndex = 0;
            // 
            // txtOld
            // 
            this.txtOld.AcceptsTab = true;
            this.txtOld.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOld.Location = new System.Drawing.Point(15, 15);
            this.txtOld.Name = "txtOld";
            this.txtOld.Size = new System.Drawing.Size(473, 374);
            this.txtOld.TabIndex = 0;
            this.txtOld.Text = resources.GetString("txtOld.Text");
            // 
            // txtNew
            // 
            this.txtNew.AcceptsTab = true;
            this.txtNew.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNew.Location = new System.Drawing.Point(15, 15);
            this.txtNew.Name = "txtNew";
            this.txtNew.Size = new System.Drawing.Size(475, 374);
            this.txtNew.TabIndex = 1;
            this.txtNew.Text = resources.GetString("txtNew.Text");
            // 
            // splitContainer2
            // 
            this.splitContainer2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.btnCompare);
            this.splitContainer2.Panel1MinSize = 10;
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.trView);
            this.splitContainer2.Panel2.Margin = new System.Windows.Forms.Padding(5);
            this.splitContainer2.Panel2.Padding = new System.Windows.Forms.Padding(250, 20, 250, 20);
            this.splitContainer2.Panel2MinSize = 50;
            this.splitContainer2.Size = new System.Drawing.Size(1012, 193);
            this.splitContainer2.SplitterDistance = 41;
            this.splitContainer2.TabIndex = 0;
            // 
            // btnCompare
            // 
            this.btnCompare.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnCompare.Location = new System.Drawing.Point(399, 3);
            this.btnCompare.Name = "btnCompare";
            this.btnCompare.Size = new System.Drawing.Size(200, 34);
            this.btnCompare.TabIndex = 0;
            this.btnCompare.Text = "Compare Methods";
            this.btnCompare.UseVisualStyleBackColor = true;
            this.btnCompare.Click += new System.EventHandler(this.OnCompareClick);
            // 
            // trView
            // 
            this.trView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trView.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.trView.Location = new System.Drawing.Point(250, 20);
            this.trView.Name = "trView";
            this.trView.Size = new System.Drawing.Size(512, 108);
            this.trView.TabIndex = 0;
            this.trView.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.OnNodeDoubleClick);
            // 
            // GuiDemo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1012, 601);
            this.Controls.Add(this.splitContainer1);
            this.Name = "GuiDemo";
            this.Text = "GuiDemo";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.TreeView trView;
        private System.Windows.Forms.Button btnCompare;
        private System.Windows.Forms.RichTextBox txtOld;
        private System.Windows.Forms.RichTextBox txtNew;



    }
}