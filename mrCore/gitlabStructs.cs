﻿using System.Collections.Generic;

namespace mrCore
{
   public struct Author
   {
      public int Id;
      public string Name;
      public string Username;
   }

   public enum MergeRequestState
   {
      Opened,
      Closed,
      Locked,
      Merged
   }

   public struct Project
   {
      public int Id;
      public string NameWithNamespace;
   }

   public struct MergeRequest
   {
      public int Id;
      public string Title;
      public string Description;
      public string SourceBranch;
      public string TargetBranch;
      public MergeRequestState State;
      public List<string> Labels;
      public string WebUrl;
      public bool WorkInProgress;
      public Author Author;
      public string BaseSHA;
      public string HeadSHA;
      public string StartSHA;
   }

   public struct Commit
   {
      public string Id;
      public string ShortId;
      public string Title;
      public string Message;
      public System.DateTime CommitedDate;
   }

   public struct Version
   {
      public int Id;
      public string HeadSHA;
      public string BaseSHA;
      public string StartSHA;
      public System.DateTime CreatedAt;
   }
}