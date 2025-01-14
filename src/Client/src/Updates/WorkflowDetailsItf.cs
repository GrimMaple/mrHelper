using System;
using System.Collections.Generic;
using GitLabSharp.Entities;

namespace mrHelper.Client.Updates
{
   internal struct ProjectKey
   {
      public string HostName;
      public int ProjectId;
   }

   internal interface IWorkflowDetails
   {
      /// <summary>
      /// Create a copy of object
      /// </summary>
      IWorkflowDetails Clone();

      /// <summary>
      /// Return project name (Path_With_Namespace) by unique project Id
      /// </summary>
      string GetProjectName(ProjectKey key);

      /// <summary>
      /// Return a list of merge requests by unique project id
      /// </summary>
      List<MergeRequest> GetMergeRequests(ProjectKey key);

      /// <summary>
      /// Return a timestamp of the most recent version of a specified merge request
      /// </summary>
      DateTime GetLatestChangeTimestamp(int mergeRequestId);

      /// <summary>
      /// Return project Id by merge request Id
      /// </summary>
      ProjectKey GetProjectKey(int mergeRequestId);
   }
}

