﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mrHelper
{
   public struct MergeRequestDetails
   {
      public string Host;
      public string AccessToken;
      public string Project;
      public int Id;
      public string BaseSHA;
      public string StartSHA;
      public string HeadSHA;
      public string TempFolder;
   }

   public struct DiffDetails
   {
      public string FilenameCurrentPane;
      public string FilenameNextPane;
      public string LineNumberCurrentPane;
      public string LineNumberNextPane;
   }

   public struct PositionDetails
   {
      public string OldFilename;
      public string OldLineNumber;
      public string NewFilename;
      public string NewLineNumber;
      public bool Ambiguous;

      public PositionDetails(string oldFilename, string oldLineNumber, string newFilename, string newLineNumber,
         bool ambiguous)
      {
         OldFilename = oldFilename;
         OldLineNumber = oldLineNumber;
         NewFilename = newFilename;
         NewLineNumber = newLineNumber;
         Ambiguous = ambiguous;
      }
   }

   public partial class NewDiscussionForm : Form
   {
      static Regex trimmedFilenameRe = new Regex(@".*\/(right|left)\/(.*)", RegexOptions.Compiled);
      static Regex diffSectionRe = new Regex(@"\@\@\s-(?'left_start'\d+)(,(?'left_len'\d+))?\s\+(?'right_start'\d+)(,(?'right_len'\d+))?\s\@\@", RegexOptions.Compiled);

      public NewDiscussionForm(MergeRequestDetails mrDetails, DiffDetails diffDetails)
      {
         _mergeRequestDetails = mrDetails;
         _diffDetails = diffDetails;

         InitializeComponent();
         onApplicationStarted();
      }

      private void onApplicationStarted()
      {
         //textBoxFileName.Text = _diffDetails.FilenameRight;
         //textBoxLineNumber.Text = _diffDetails.LineNumberRight;
         textBoxContext.Text = getDiscussionContext();
      }

      private void ButtonOK_Click(object sender, EventArgs e)
      {
         if (textBoxDiscussionBody.Text.Length == 0)
         {
            MessageBox.Show("Discussion body cannot be empty", "Warning",
               MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
         }

         gitlabClient client = new gitlabClient(_mergeRequestDetails.Host, _mergeRequestDetails.AccessToken);
         DiscussionParameters parameters = getDiscussionParameters();

         try
         {
            client.CreateNewMergeRequestDiscussion(
               _mergeRequestDetails.Project, _mergeRequestDetails.Id, parameters);
         }
         catch (System.Net.WebException ex)
         {
            MessageBox.Show(ex.Message +
               "Cannot create a new discussion. Gitlab does not accept passed line numbers.",
               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
      }

      private DiscussionParameters getDiscussionParameters()
      {
         DiscussionParameters parameters = new DiscussionParameters();
         parameters.Body = textBoxDiscussionBody.Text;
         if (!checkBoxIncludeContext.Checked)
         {
            return parameters;
         }

         DiscussionParameters.PositionDetails details =
            new DiscussionParameters.PositionDetails();
         details.BaseSHA = _mergeRequestDetails.BaseSHA;
         details.HeadSHA = _mergeRequestDetails.HeadSHA;
         details.StartSHA = _mergeRequestDetails.StartSHA;

         string path = convertToGitlabFilename(_diffDetails.FilenameCurrentPane);
         PositionDetails positionDetails = getPositionDetails(
            convertToGitlabFilename(_diffDetails.FilenameCurrentPane), int.Parse(_diffDetails.LineNumberCurrentPane),
            convertToGitlabFilename(_diffDetails.FilenameNextPane), int.Parse(_diffDetails.LineNumberNextPane));

         details.OldPath = positionDetails.OldFilename;
         details.OldLine = positionDetails.OldLineNumber;
         details.NewPath = positionDetails.NewFilename;
         details.NewLine = positionDetails.NewLineNumber;
         parameters.Position = details;

         return parameters;
      }

      private string convertToGitlabFilename(string fullFilename)
      {
         string trimmedFilename = fullFilename
            .Substring(_mergeRequestDetails.TempFolder.Length, // TODO does it work?
               _diffDetails.FilenameCurrentPane.Length - _mergeRequestDetails.TempFolder.Length)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

         Match m = trimmedFilenameRe.Match(trimmedFilename);
         if (!m.Success)
         {
            throw new ApplicationException("Cannot parse a path obtained from difftool");
         }

         return m.Groups[2].Value;
      }

      private void ButtonCancel_Click(object sender, EventArgs e)
      {
         Close();
      }

      private string getDiscussionContext()
      {
         // TODO
         //   return File.ReadLines(filename).Skip(lineNumber - 1).Take(1).First();
         return "";
      }

      private PositionDetails getPositionDetails(string filenameCurrentPane, int lineNumberCurrentPane,
         string filenameNextPane, int lineNumberNextPane)
      {
         List<string> diff = gitClient.Diff(_mergeRequestDetails.StartSHA, _mergeRequestDetails.HeadSHA,
            filenameCurrentPane); // TODO - It is always current, is it ok?
         foreach (string line in diff)
         {
            Match m = diffSectionRe.Match(line);
            if (!m.Success)
            {
               continue;
            }

            if (!m.Groups["left_start"].Success || !m.Groups["right_start"].Success)
            {
               continue;
            }

            int leftSectionStart = int.Parse(m.Groups["left_start"].Value);
            int leftSectionLength = !m.Groups["left_len"].Success ? int.Parse(m.Groups["left_len"].Value) : 1;
            int leftSectionEnd = leftSectionStart + leftSectionLength;
            int rightSectionStart = int.Parse(m.Groups["right_start"].Value);
            int rightSectionLength = !m.Groups["right_len"].Success ? int.Parse(m.Groups["right_len"].Value) : 1;
            int rightSectionEnd = rightSectionStart + rightSectionLength;

            bool currentAtLeft = lineNumberCurrentPane >= leftSectionStart && lineNumberCurrentPane < leftSectionEnd;
            bool currentAtRight = lineNumberCurrentPane >= rightSectionStart && lineNumberCurrentPane < rightSectionEnd;
            bool nextAtLeft = lineNumberNextPane >= leftSectionStart && lineNumberNextPane < leftSectionEnd;
            bool nextAtRight = lineNumberNextPane >= rightSectionStart && lineNumberNextPane < rightSectionEnd;

            if (currentAtLeft)
            {
               if (nextAtRight)
               {
                  return new PositionDetails(null, null, filenameCurrentPane, lineNumberCurrentPane.ToString(), true);
               }
               else
               {
                  return new PositionDetails(filenameCurrentPane, lineNumberCurrentPane.ToString(), null, null, false);
               }
            }
            else if (currentAtRight)
            {
               if (nextAtLeft)
               {
                  return new PositionDetails(null, null, filenameCurrentPane, lineNumberCurrentPane.ToString(), true);
               }
               else
               {
                  return new PositionDetails(null, null, filenameCurrentPane, lineNumberCurrentPane.ToString(), false);
               }
            }
            else if (nextAtLeft) // current not found
            {
               return new PositionDetails(filenameNextPane, lineNumberNextPane.ToString(), null, null, false);
            }
            else if (nextAtRight) // current not found
            {
               return new PositionDetails(null, null, filenameNextPane, lineNumberNextPane.ToString(), false);
            }
            else
            {
               continue; // check next diff section
            }
         }

         return new PositionDetails(filenameCurrentPane, lineNumberCurrentPane.ToString(),
            filenameNextPane, lineNumberNextPane.ToString(), true);
      }

      private readonly MergeRequestDetails _mergeRequestDetails;
      private readonly DiffDetails _diffDetails;
   }
}
