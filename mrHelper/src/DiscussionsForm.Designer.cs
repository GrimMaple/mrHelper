﻿namespace mrHelperUI
{
   partial class DiscussionsForm
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
         this.components = new System.ComponentModel.Container();
         System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DiscussionsForm));
         this.toolTip = new System.Windows.Forms.ToolTip(this.components);
         this.toolTipNotifier = new System.Windows.Forms.ToolTip(this.components);
         this.SuspendLayout();
         // 
         // toolTip
         // 
         this.toolTip.AutoPopDelay = 5000;
         this.toolTip.InitialDelay = 500;
         this.toolTip.ReshowDelay = 100;
         // 
         // DiscussionsForm
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.AutoScroll = true;
         this.ClientSize = new System.Drawing.Size(1353, 456);
         this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
         this.KeyPreview = true;
         this.Name = "DiscussionsForm";
         this.Text = "Discussions";
         this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
         this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.DiscussionsForm_KeyDown);
         this.Resize += new System.EventHandler(this.DiscussionsForm_Resize);
         this.ResumeLayout(false);

      }

      #endregion

      private System.Windows.Forms.ToolTip toolTip;
      private System.Windows.Forms.ToolTip toolTipNotifier;
      private TheArtOfDev.HtmlRenderer.WinForms.HtmlToolTip htmlToolTip = new TheArtOfDev.HtmlRenderer.WinForms.HtmlToolTip();
   }
}