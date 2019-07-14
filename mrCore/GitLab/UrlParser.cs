﻿using System;
using System.Text.RegularExpressions;

namespace mrCore
{
   public struct ParsedMergeRequestUrl
   {
      public string Host;
      public string Project;
      public int Id;
   }

   internal class MergeRequestUrlParser
   {
      private static readonly Regex url_re = new Regex(
         @"^(http[s]?):\/\/([^:\/\s]+)\/(api\/v4\/projects\/)?(\w+\/\w+)\/merge_requests\/(\d*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

      public MergeRequestUrlParser(string url)
      {
         _url = url;
      }

      public ParsedMergeRequestUrl Parse()
      {
         if (!Uri.IsWellFormedUriString(_url, UriKind.Absolute))
         {
            throw new UriFormatException("Wrong URL format");
         }

         Match m = url_re.Match(_url);
         if (!m.Success)
         {
            throw new UriFormatException("Failed");
         }

         if (m.Groups.Count < 5)
         {
            throw new UriFormatException("Unsupported merge requests URL format");
         }

         ParsedMergeRequestUrl result = new ParsedMergeRequestUrl
         {
            Host = m.Groups[2].Value,
            Project = m.Groups[4].Value,
            Id = int.Parse(m.Groups[5].Value)
         };
         return result;
      }

      private readonly string _url;
   }
}
