﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using TheArtOfDev.HtmlRenderer.WinForms;
using GitLabSharp.Entities;
using mrHelper.App.Forms;
using mrHelper.App.Helpers;
using mrHelper.App.Controls;
using mrHelper.Common.Interfaces;
using mrHelper.Common.Exceptions;
using mrHelper.Client.Tools;
using mrHelper.Client.Discussions;
using mrHelper.Core.Context;
using mrHelper.Core.Matching;

namespace mrHelper.App.Controls
{
   internal class DiscussionBox : Panel
   {
      private const int EM_GETLINECOUNT = 0xba;
      [DllImport("user32", EntryPoint = "SendMessageA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
      private static extern int SendMessage(int hwnd, int wMsg, int wParam, int lParam);

      internal DiscussionBox(Discussion discussion, DiscussionEditor editor, User mergeRequestAuthor, User currentUser,
         int diffContextDepth, IGitRepository gitRepository, ColorScheme colorScheme,
         Action<DiscussionBox> preContentChange, Action<DiscussionBox> onContentChanged)
      {
         Discussion = discussion;
         _editor = editor;
         _mergeRequestAuthor = mergeRequestAuthor;
         _currentUser = currentUser;

         _diffContextDepth = new ContextDepth(0, diffContextDepth);
         _tooltipContextDepth = new ContextDepth(5, 5);
         _formatter = new DiffContextFormatter();
         if (gitRepository != null)
         {
            _panelContextMaker = new EnhancedContextMaker(gitRepository);
            _tooltipContextMaker = new CombinedContextMaker(gitRepository);
         }
         _colorScheme = colorScheme;

         _preContentChange = preContentChange;
         _onContentChanged = onContentChanged;

         _toolTip = new ToolTip
         {
            AutoPopDelay = 5000,
            InitialDelay = 500,
            ReshowDelay = 100
         };

         _toolTipNotifier = new ToolTip();

         _htmlToolTip = new HtmlToolTip
         {
            AutoPopDelay = 10000, // 10s
            BaseStylesheet = ".htmltooltip { padding: 1px; }"
         };

         onCreate();
      }

      internal Discussion Discussion { get; private set; }

      private void TextBox_KeyDown(object sender, KeyEventArgs e)
      {
         TextBox textBox = (TextBox)(sender);

         if (textBox.ReadOnly && e.KeyData == Keys.F2)
         {
            DiscussionNote note = (DiscussionNote)(textBox.Tag);
            if (canBeModified(note))
            {
               onStartEditNote(textBox);
            }
         }
         else if (!textBox.ReadOnly && e.KeyData == Keys.Escape)
         {
            onCancelEditNote(textBox);
            updateTextboxHeight(textBox);
         }
      }

      private void TextBox_KeyUp(object sender, KeyEventArgs e)
      {
         TextBox textBox = (TextBox)(sender);

         if (!textBox.ReadOnly)
         {
            updateTextboxHeight(textBox);
         }
      }

      private void updateTextboxHeight(Control textBox)
      {
         int newHeight = getTextBoxPreferredHeight(textBox);
         if (newHeight != textBox.Height)
         {
            textBox.Height = newHeight;
            _onContentChanged(this);
         }
      }

      async private void TextBox_LostFocus(object sender, EventArgs e)
      {
         TextBox textBox = (TextBox)(sender);
         if (textBox.ReadOnly)
         {
            return;
         }

         await onSubmitNewBodyAsync(textBox);
      }

      async private void MenuItemReply_Click(object sender, EventArgs e)
      {
         MenuItem menuItem = (MenuItem)(sender);
         TextBox textBox = (TextBox)(menuItem.Tag);
         if (textBox?.Parent?.Parent == null)
         {
            return;
         }

         using (NewDiscussionItemForm form = new NewDiscussionItemForm("Reply to Discussion"))
         {
            if (form.ShowDialog() == DialogResult.OK)
            {
               if (form.Body.Length == 0)
               {
                  MessageBox.Show("Reply text cannot be empty", "Warning",
                     MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                  return;
               }

               await onReplyAsync(form.Body);
            }
         }
      }

      private void MenuItemEditNote_Click(object sender, EventArgs e)
      {
         MenuItem menuItem = (MenuItem)(sender);
         TextBox textBox = (TextBox)(menuItem.Tag);
         if (textBox?.Parent?.Parent == null)
         {
            return;
         }

         onStartEditNote(textBox);
      }

      async private void MenuItemDeleteNote_Click(object sender, EventArgs e)
      {
         MenuItem menuItem = (MenuItem)(sender);
         TextBox textBox = (TextBox)(menuItem.Tag);
         if (textBox?.Parent?.Parent == null)
         {
            return;
         }

         textBox.ReadOnly = true; // prevent submitting body modifications in the current handler

         if (MessageBox.Show("This discussion note will be deleted. Are you sure?", "Confirm deletion",
               MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
         {
            return;
         }

         await onDeleteNoteAsync(textBox);
      }

      async private void MenuItemToggleResolveNote_Click(object sender, EventArgs e)
      {
         MenuItem menuItem = (MenuItem)(sender);
         TextBox textBox = (TextBox)(menuItem.Tag);
         if (textBox?.Parent?.Parent == null)
         {
            return;
         }

         textBox.ReadOnly = true; // prevent submitting body modifications in the current handler

         DiscussionNote note = (DiscussionNote)(textBox.Tag);
         Debug.Assert(note.Resolvable);

         await onToggleResolveNoteAsync(textBox);
      }

      async private void MenuItemToggleResolveDiscussion_Click(object sender, EventArgs e)
      {
         MenuItem menuItem = (MenuItem)(sender);
         TextBox textBox = (TextBox)(menuItem.Tag);
         if (textBox?.Parent?.Parent == null)
         {
            return;
         }

         textBox.ReadOnly = true; // prevent submitting body modifications in the current handler

         DiscussionNote note = (DiscussionNote)(textBox.Tag);
         Debug.Assert(note.Resolvable);

         await onToggleResolveDiscussionAsync();
      }

      internal Size AdjustToWidth(int width)
      {
         resizeBoxContent(width);
         repositionBoxContent(width);
         return Size;
      }

      private void onCreate()
      {
         Debug.Assert(Discussion.Notes.Count > 0);

         var firstNote = Discussion.Notes[0];

         _labelAuthor = createLabelAuthor(firstNote);
         _labelFileName = createLabelFilename(firstNote);
         _panelContext = createDiffContext(firstNote);
         _textboxesNotes = createTextBoxes(Discussion.Notes);

         Controls.Add(_labelAuthor);
         Controls.Add(_labelFileName);
         Controls.Add(_panelContext);
         foreach (var note in _textboxesNotes)
         {
            Controls.Add(note);
         }
      }

      private HtmlPanel createDiffContext(DiscussionNote firstNote)
      {
         if (firstNote.Type != "DiffNote")
         {
            return null;
         }

         int fontSizePx = 12;
         int rowsVPaddingPx = 2;
         int rowHeight = (fontSizePx + rowsVPaddingPx * 2 + 1 /* border of control */ + 2);
         // we're adding 2 extra pixels for each row because HtmlRenderer does not support CSS line-height property
         // this value was found experimentally

         int panelHeight = (_diffContextDepth.Size + 1) * rowHeight;

         HtmlPanel htmlPanel = new HtmlPanel
         {
            BorderStyle = BorderStyle.FixedSingle,
            Height = panelHeight,
            MinimumSize = new Size(600, 0),
            TabStop = false
         };

         DiffPosition position = convertToDiffPosition(firstNote.Position);
         htmlPanel.Text = getContext(_panelContextMaker, position,
            _diffContextDepth, fontSizePx, rowsVPaddingPx);
         _htmlToolTip.SetToolTip(htmlPanel, getContext(_tooltipContextMaker, position,
            _tooltipContextDepth, fontSizePx, rowsVPaddingPx));

         return htmlPanel;
      }

      private string getContext(IContextMaker contextMaker, DiffPosition position,
         ContextDepth depth, int fontSizePx, int rowsVPaddingPx)
      {
         if (contextMaker == null || _formatter == null)
         {
            return "<html><body>Cannot access git repository and render diff context</body></html>";
         }

         try
         {
            DiffContext context = contextMaker.GetContext(position, depth);
            return _formatter.FormatAsHTML(context, fontSizePx, rowsVPaddingPx);
         }
         catch (Exception ex)
         {
            if (ex is ArgumentException || ex is GitOperationException || ex is GitObjectException)
            {
               ExceptionHandlers.Handle(ex, "Cannot render HTML context");
               string errorMessage = System.Net.WebUtility.HtmlEncode(ex.Message).Replace("\n", "<br>");
               return String.Format("<html><body>{0}</body></html>", errorMessage);
            }
            throw;
         }
      }

      // Create a label that shows filename
      private Control createLabelFilename(DiscussionNote firstNote)
      {
         if (firstNote.Type != "DiffNote")
         {
            return null;
         }

         string oldPath = firstNote.Position.Old_Path + " (line " + firstNote.Position.Old_Line + ")";
         string newPath = firstNote.Position.New_Path + " (line " + firstNote.Position.New_Line + ")";

         string result;
         if (firstNote.Position.Old_Line == null)
         {
            result = newPath;
         }
         else if (firstNote.Position.New_Line == null)
         {
            result = oldPath;
         }
         else if (firstNote.Position.Old_Path == firstNote.Position.New_Path)
         {
            result = newPath;
         }
         else
         {
            result = newPath + "\r\n(was " + oldPath + ")";
         }

         TextBox labelFilename = new TextBoxNoWheel
         {
            ReadOnly = true,
            Text = result,
            Multiline = true,
            MinimumSize = new Size(300, 0)
         };
         labelFilename.Height = getTextBoxPreferredHeight(labelFilename);
         return labelFilename;
      }

      // Create a label that shows discussion author
      private Control createLabelAuthor(DiscussionNote firstNote)
      {
         Label labelAuthor = new Label
         {
            Text = firstNote.Author.Name,
            AutoEllipsis = true,
            MinimumSize = new Size(100, 0)
         };
         return labelAuthor;
      }

      private List<Control> createTextBoxes(List<DiscussionNote> notes)
      {
         var discussionResolved = notes.Cast<DiscussionNote>().All(x => (!x.Resolvable || x.Resolved));

         List<Control> boxes = new List<Control>();
         foreach (var note in notes)
         {
            if (note.System)
            {
               // skip spam
               continue;
            }

            Control textBox = createTextBox(note, discussionResolved);
            boxes.Add(textBox);
         }
         return boxes;
      }

      private bool canBeModified(DiscussionNote note)
      {
         return note.Author.Id == _currentUser.Id && (!note.Resolvable || !note.Resolved);
      }

      private Control createTextBox(DiscussionNote note, bool discussionResolved)
      {
         TextBox textBox = new TextBoxNoWheel();
         _toolTip.SetToolTip(textBox, getNoteTooltipText(note));
         textBox.ReadOnly = true;
         textBox.Text = note.Body.Replace("\n", "\r\n");
         textBox.Multiline = true;
         textBox.Height = getTextBoxPreferredHeight(textBox);
         textBox.BackColor = getNoteColor(note);
         textBox.LostFocus += TextBox_LostFocus;
         textBox.KeyDown += TextBox_KeyDown;
         textBox.KeyUp += TextBox_KeyUp;
         textBox.MinimumSize = new Size(300, 0);
         textBox.Tag = note;
         textBox.ContextMenu = createContextMenuForDiscussionNote(note, discussionResolved, textBox);

         return textBox;
      }

      private ContextMenu createContextMenuForDiscussionNote(DiscussionNote note,
         bool discussionResolved, TextBox textBox)
      {
         var contextMenu = new ContextMenu();

         MenuItem menuItemToggleDiscussionResolve = new MenuItem
         {
            Tag = textBox,
            Text = (discussionResolved ? "Unresolve" : "Resolve") + " Discussion",
            Enabled = note.Resolvable
         };
         menuItemToggleDiscussionResolve.Click += MenuItemToggleResolveDiscussion_Click;
         contextMenu.MenuItems.Add(menuItemToggleDiscussionResolve);

         MenuItem menuItemToggleResolve = new MenuItem
         {
            Tag = textBox,
            Text = (note.Resolvable && note.Resolved ? "Unresolve" : "Resolve") + " Note",
            Enabled = note.Resolvable
         };
         menuItemToggleResolve.Click += MenuItemToggleResolveNote_Click;
         contextMenu.MenuItems.Add(menuItemToggleResolve);

         MenuItem menuItemDeleteNote = new MenuItem
         {
            Tag = textBox,
            Enabled = canBeModified(note),
            Text = "Delete Note"
         };
         menuItemDeleteNote.Click += MenuItemDeleteNote_Click;
         contextMenu.MenuItems.Add(menuItemDeleteNote);

         MenuItem menuItemEditNote = new MenuItem
         {
            Tag = textBox,
            Enabled = canBeModified(note),
            Text = "Edit Note (F2)"
         };
         menuItemEditNote.Click += MenuItemEditNote_Click;
         contextMenu.MenuItems.Add(menuItemEditNote);

         MenuItem menuItemReply = new MenuItem
         {
            Tag = textBox,
            Enabled = !Discussion.Individual_Note,
            Text = "Reply"
         };
         menuItemReply.Click += MenuItemReply_Click;
         contextMenu.MenuItems.Add(menuItemReply);

         return contextMenu;
      }

      private static int getTextBoxPreferredHeight(Control textBox)
      {
         var numberOfLines = SendMessage(textBox.Handle.ToInt32(), EM_GETLINECOUNT, 0, 0);
         return textBox.Font.Height * (numberOfLines + 1);
      }

      private string getNoteTooltipText(DiscussionNote note)
      {
         string result = string.Empty;
         if (note.Resolvable)
         {
            result += note.Resolved ? "Resolved." : "Not resolved.";
         }
         result += " Created by " + note.Author.Name + " at " + note.Created_At.ToLocalTime().ToString("g");
         return result;
      }

      private Color getNoteColor(DiscussionNote note)
      {
         Color defaultColor = Color.White;

         if (note.Resolvable)
         {
            if (note.Author.Id == _mergeRequestAuthor.Id)
            {
               return note.Resolved
                  ? _colorScheme.GetColorOrDefault("Discussions_Author_Notes_Resolved", defaultColor)
                  : _colorScheme.GetColorOrDefault("Discussions_Author_Notes_Unresolved", defaultColor);
            }
            else
            {
               return note.Resolved
                  ? _colorScheme.GetColorOrDefault("Discussions_NonAuthor_Notes_Resolved", defaultColor)
                  : _colorScheme.GetColorOrDefault("Discussions_NonAuthor_Notes_Unresolved", defaultColor);
            }
         }
         else
         {
            return _colorScheme.GetColorOrDefault("Discussions_Comments", defaultColor);
         }
      }

      private void resizeBoxContent(int width)
      {
         foreach (var textbox in _textboxesNotes)
         {
            textbox.Width = width * NotesWidth / 100;
            textbox.Height = getTextBoxPreferredHeight(textbox);
         }

         if (_panelContext != null)
         {
            _panelContext.Width = width * ContextWidth / 100;
            _htmlToolTip.MaximumSize = new Size(_panelContext.Width, 0 /* auto-height */);
         }
         _labelAuthor.Width = width * LabelAuthorWidth / 100;
         if (_labelFileName != null)
         {
            _labelFileName.Width = width * LabelFilenameWidth / 100;
            _labelFileName.Height = getTextBoxPreferredHeight(_labelFileName);
         }
      }

      private void repositionBoxContent(int width)
      {
         int interControlVertMargin = 5;
         int interControlHorzMargin = width * HorzMarginWidth / 100;

         // the LabelAuthor is placed at the left side
         Point labelPos = new Point(interControlHorzMargin, interControlVertMargin);
         _labelAuthor.Location = labelPos;

         // the Context is an optional control to the right of the Label
         Point ctxPos = new Point(_labelAuthor.Location.X + _labelAuthor.Width + interControlHorzMargin,
            interControlVertMargin);
         if (_panelContext != null)
         {
            _panelContext.Location = ctxPos;
         }

         // prepare initial position for controls that places to the right of the Context
         int nextNoteX = ctxPos.X + (_panelContext == null ? 0 : _panelContext.Width + interControlHorzMargin);
         Point nextNotePos = new Point(nextNoteX, ctxPos.Y);

         // the LabelFilename is placed to the right of the Context and vertically aligned with Notes
         if (_labelFileName != null)
         {
            _labelFileName.Location = nextNotePos;
            nextNotePos.Offset(0, _labelFileName.Height + interControlVertMargin);
         }

         // a list of Notes is to the right of the Context
         foreach (var note in _textboxesNotes)
         {
            note.Location = nextNotePos;
            nextNotePos.Offset(0, note.Height + interControlVertMargin);
         }

         int lblAuthorHeight = _labelAuthor.Location.Y + _labelAuthor.PreferredSize.Height;
         int lblFNameHeight = (_labelFileName == null ? 0 : _labelFileName.Location.Y + _labelFileName.Height);
         int ctxHeight = (_panelContext == null ? 0 : _panelContext.Location.Y + _panelContext.Height);
         int notesHeight = _textboxesNotes[_textboxesNotes.Count - 1].Location.Y
                         + _textboxesNotes[_textboxesNotes.Count - 1].Height;

         int boxContentWidth = nextNoteX + _textboxesNotes[0].Width;
         int boxContentHeight = new[] { lblAuthorHeight, lblFNameHeight, ctxHeight, notesHeight }.Max();
         Size = new Size(boxContentWidth + interControlHorzMargin, boxContentHeight + interControlVertMargin);
      }

      async private Task onReplyAsync(string body)
      {
         try
         {
            await _editor.ReplyAsync(body);
         }
         catch (DiscussionEditorException)
         {
            MessageBox.Show("Cannot create a reply to discussion");
            return;
         }

         await refreshDiscussion();
      }

      private void onStartEditNote(TextBox textBox)
      {
         textBox.ReadOnly = false;
         textBox.Focus();
      }

      private void onCancelEditNote(TextBox textBox)
      {
         textBox.ReadOnly = true;

         DiscussionNote note = (DiscussionNote)(textBox.Tag);
         textBox.Text = note.Body.Replace("\n", "\r\n");
      }

      async private Task onSubmitNewBodyAsync(TextBox textBox)
      {
         textBox.ReadOnly = true;

         DiscussionNote note = (DiscussionNote)(textBox.Tag);
         if (textBox.Text == note.Body)
         {
            return;
         }

         if (textBox.Text.Length == 0)
         {
            MessageBox.Show("Discussion text cannot be empty", "Warning",
               MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            onCancelEditNote(textBox);
            return;
         }

         note.Body = textBox.Text;

         try
         {
            await _editor.ModifyNoteBodyAsync(note.Id, note.Body);
         }
         catch (DiscussionEditorException)
         {
            MessageBox.Show("Cannot update discussion text", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         if (!textBox.IsDisposed)
         {
            _toolTipNotifier.Show("Discussion note was edited", textBox, textBox.Width + 20, 0, 2000 /* ms */);
         }
      }

      async private Task onDeleteNoteAsync(TextBox textBox)
      {
         DiscussionNote note = (DiscussionNote)(textBox.Tag);

         try
         {
            await _editor.DeleteNoteAsync(note.Id);
         }
         catch (DiscussionEditorException)
         {
            MessageBox.Show("Cannot delete a note", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         await refreshDiscussion();
      }

      async private Task onToggleResolveNoteAsync(TextBox textBox)
      {
         DiscussionNote note = (DiscussionNote)(textBox.Tag);
         bool wasResolved = note.Resolved;

         try
         {
            await _editor.ResolveNoteAsync(note.Id, !wasResolved);
         }
         catch (DiscussionEditorException)
         {
            MessageBox.Show("Cannot toggle 'Resolved' state of a note", "Error",
               MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         await refreshDiscussion();
      }

      async private Task onToggleResolveDiscussionAsync()
      {
         bool wasResolved = isDiscussionResolved();

         try
         {
            await _editor.ResolveDiscussionAsync(!wasResolved);
         }
         catch (DiscussionEditorException)
         {
            MessageBox.Show("Cannot toggle 'Resolved' state of a discussion", "Error",
               MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }

         await refreshDiscussion();
      }

      async private Task refreshDiscussion()
      {
         if (Parent == null)
         {
            return;
         }

         _preContentChange(this);

         // Get rid of old text boxes
         for (int iControl = Controls.Count - 1; iControl >= 0; --iControl)
         {
            if (_textboxesNotes.IndexOf(Controls[iControl]) != -1)
            {
               Controls.Remove(Controls[iControl]);
            }
         }
         _textboxesNotes.Clear();

         // Load updated discussion
         try
         {
            Discussion = await _editor.GetDiscussion();
         }
         catch (DiscussionEditorException)
         {
            // it is not an error here, we treat it as 'last discussion item has been deleted'
            // Seems it was the only note in the discussion, remove ourselves from parents controls
            Parent.Controls.Remove(this);
            _onContentChanged(this);
            return;
         }

         if (Discussion.Notes.Count == 0 || Discussion.Notes[0].System)
         {
            // It happens when Discussion has System notes like 'a line changed ...'
            // along with a user note that has been just deleted
            Parent.Controls.Remove(this);
            _onContentChanged(this);
            return;
         }

         // Create controls
         _textboxesNotes = createTextBoxes(Discussion.Notes);
         foreach (var note in _textboxesNotes)
         {
            Controls.Add(note);
         }

         // To reposition new controls
         _onContentChanged(this);
      }

      private bool isDiscussionResolved()
      {
         bool result = true;
         foreach (Control textBox in _textboxesNotes)
         {
            DiscussionNote note = (DiscussionNote)(textBox.Tag);
            if (note.Resolvable && !note.Resolved)
            {
               result = false;
            }
         }
         return result;
      }

      private DiffPosition convertToDiffPosition(Position position)
      {
         return new DiffPosition
         {
            LeftLine = position.Old_Line,
            LeftPath = position.Old_Path,
            RightLine = position.New_Line,
            RightPath = position.New_Path,
            Refs = new mrHelper.Core.Matching.DiffRefs
            {
               LeftSHA = position.Base_SHA,
               RightSHA = position.Head_SHA
            }
         };
      }

      // Widths in %
      private readonly int HorzMarginWidth = 1;
      private readonly int LabelAuthorWidth = 5;
      private readonly int ContextWidth = 55;
      private readonly int NotesWidth = 34;
      private readonly int LabelFilenameWidth = 34;

      private Control _labelAuthor;
      private Control _labelFileName;
      private Control _panelContext;
      private List<Control> _textboxesNotes;

      private readonly User _mergeRequestAuthor;
      private readonly User _currentUser;

      private readonly ContextDepth _diffContextDepth;
      private readonly ContextDepth _tooltipContextDepth;
      private readonly IContextMaker _panelContextMaker;
      private readonly IContextMaker _tooltipContextMaker;
      private readonly DiffContextFormatter _formatter;
      private readonly DiscussionEditor _editor;

      private readonly ColorScheme _colorScheme;

      private readonly Action<DiscussionBox> _preContentChange;
      private readonly Action<DiscussionBox> _onContentChanged;

      private readonly System.Windows.Forms.ToolTip _toolTip;
      private readonly System.Windows.Forms.ToolTip _toolTipNotifier;
      private readonly TheArtOfDev.HtmlRenderer.WinForms.HtmlToolTip _htmlToolTip;
   }
}
