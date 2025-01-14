﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mrHelper.App.Controls
{
   /// <summary>
   /// Unlike its parent class, SelectionPreservingComboBox preserves state of SelectedIndex, SelectedItem,
   /// SelectedText and Text properties until SelectedIndexChanged event is invoked, including cases
   /// when dropdown is expanded and user hovers but does not click on items
   /// </summary>
   internal class SelectionPreservingComboBox : ComboBox
   {
      new internal event EventHandler SelectedIndexChanged;

      internal SelectionPreservingComboBox()
      {
         base.SelectionChangeCommitted +=
            (sender, args) =>
         {
            SelectedIndex = base.SelectedIndex;
         };

         base.SelectedIndexChanged +=
            (sender, args) =>
         {
            SelectedIndexChanged?.Invoke(sender, args);
         };
      }

      new internal int SelectedIndex
      {
         get
         {
            return _selectedIndex;
         }

         set
         {
            _selectedIndex = value;
            if (_selectedIndex > Items.Count - 1)
            {
               _selectedIndex = -1;
            }
            base.SelectedIndex = _selectedIndex;
            base.SelectedItem = SelectedItem;
            base.SelectedText = SelectedText;
            base.Text = Text;
         }
      }

      new internal object SelectedItem
      {
         get
         {
            return _selectedIndex == -1 ? null : Items[_selectedIndex];
         }
         set
         {
            SelectedIndex = Items.IndexOf(value);
         }
      }

      new internal string SelectedText
      {
         get
         {
            return _selectedIndex == -1 ? "" : GetItemText(Items[_selectedIndex]);
         }
      }

      new internal string Text
      {
         get
         {
            if (DropDownStyle == ComboBoxStyle.DropDown && _customText != null)
            {
               return _customText;
            }
            return _selectedIndex == -1 ? "" : GetItemText(Items[_selectedIndex]);
         }
         set
         {
            _customText = value;
            base.Text = Text;
         }
      }

      new internal ComboBoxStyle DropDownStyle
      {
         get
         {
            return base.DropDownStyle;
         }
         set
         {
            if (base.DropDownStyle == value)
            {
               return;
            }
            base.DropDownStyle = value;
            base.Text = Text;
         }
      }

      private int _selectedIndex = -1;
      private string _customText;
   }
}

