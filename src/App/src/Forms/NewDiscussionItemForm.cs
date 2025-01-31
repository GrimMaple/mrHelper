﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mrHelper.App.Forms
{
   internal partial class NewDiscussionItemForm : Form
   {
      internal NewDiscussionItemForm(string caption)
      {
         InitializeComponent();
         this.Text = caption;
      }

      internal string Body => textBox.Text;

      private void textBox_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.KeyCode == Keys.Enter && Control.ModifierKeys == Keys.Control)
         {
            e.Handled = false;

            buttonOK.PerformClick(); 
         }
      }

      private void NewDiscussionItemForm_Load(object sender, EventArgs e)
      {
         this.ActiveControl = textBox;
      }
   }
}
