﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace mrHelper.Core.Context
{
   /// <summary>
   /// Renders DiffContext objects into HTML web page using CSS from resources
   /// </summary>
   public class DiffContextFormatter
   {
      public DiffContextFormatter()
      {
         _css = loadStylesFromCSS();
      }

      /// <summary>
      /// Throws ArgumentException if DiffContext is invalid
      /// </summary>
      public string FormatAsHTML(DiffContext context, int fontSizePx = 12, int rowsVPaddingPx = 2)
      {
         return getContextHTML(context, fontSizePx, rowsVPaddingPx);
      }

      private string getContextHTML(DiffContext ctx, int fontSizePx, int rowsVPaddingPx)
      {
         string customStyle = getCustomStyle(fontSizePx, rowsVPaddingPx);

         string commonBegin = string.Format(@"
            <html>
               <head>
                  <style>{0}{1}</style>
               </head>
               <body>
                  <table cellspacing=""0"" cellpadding=""0"">
                      <tbody>", _css, customStyle);

         string commonEnd = @"
                      </tbody>
                   </table>
                </body>
             </html>";

         return commonBegin + getTableBody(ctx) + commonEnd;
      }

      private string loadStylesFromCSS()
      {
         return mrHelper.Core.Properties.Resources.DiffContextCSS;
      }

      private string getCustomStyle(int fontSizePx, int rowsVPaddingPx)
      {
         return string.Format(@"
            table {{
               font-size: {0}px;
            }}
            td {{
               padding-top: {1}px; 
               padding-bottom: {1}px; 
            }}", fontSizePx, rowsVPaddingPx);
      }

      private string getTableBody(DiffContext ctx)
      {
         bool highlightSelected = ctx.Lines.Count > 1;
         string body = string.Empty;

         for (int iLine = 0; iLine < ctx.Lines.Count; ++iLine)
         {
            var line = ctx.Lines[iLine];

            body
              += "<tr" + (iLine == ctx.SelectedIndex && highlightSelected ? " class=\"selected\"" : "") + ">"
               + "<td class=\"linenumbers\">" + getLeftLineNumber(line) + "</td>" 
               + "<td class=\"linenumbers\">" + getRightLineNumber(line) + "</td>"
               + "<td class=\"" + getDiffCellClass(line) + "\">" + getCode(line) + "</td>"
               + "</tr>";
         }
         return body;
      }

      private string getLeftLineNumber(DiffContext.Line line)
      {
         return line.Left?.Number.ToString() ?? "";
      }

      private string getRightLineNumber(DiffContext.Line line)
      {
         return line.Right?.Number.ToString() ?? "";
      }

      private string getDiffCellClass(DiffContext.Line line)
      {
         if (line.Left.HasValue && line.Right.HasValue)
         {
            return "unchanged";
         }
         else if (line.Left.HasValue)
         {
            return line.Left.Value.State == DiffContext.Line.State.Unchanged ? "unchanged" : "removed";
         }
         else if (line.Right.HasValue)
         {
            return line.Right.Value.State == DiffContext.Line.State.Unchanged ? "unchanged" : "added";
         }

         throw new ArgumentException(String.Format("Bad context line: {0}", line.ToString()));
      }

      private string getCode(DiffContext.Line line)
      {
         if (line.Text.Length == 0)
         {
            return "<br";
         }

         string trimmed = line.Text.TrimStart();
         int leadingSpaces = line.Text.Length - trimmed.Length;

         string spaces = string.Empty;
         for (int i = 0; i < leadingSpaces; ++i)
         {
            spaces += "&nbsp;";
         }

         return spaces + System.Net.WebUtility.HtmlEncode(trimmed);
      }

      private readonly string _css;
   }
}

