using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GitLabSharp.Entities;
using GitLabSharp.Accessors;
using mrHelper.Client.Tools;
using mrHelper.Common.Exceptions;

namespace mrHelper.Client.Discussions
{
   public class DiscussionCreatorException : Exception
   {
      public DiscussionCreatorException(bool handled)
      {
         Handled = handled;
      }

      public bool Handled { get; }
   }

   /// <summary>
   /// Implements logic of new discussion creation
   /// </summary>
   public class DiscussionCreator
   {
      internal DiscussionCreator(MergeRequestDescriptor mrd, DiscussionOperator discussionOperator)
      {
         DiscussionOperator = discussionOperator;
         MergeRequestDescriptor = mrd;
      }

      async public Task CreateNoteAsync(CreateNewNoteParameters parameters)
      {
         try
         {
            await DiscussionOperator.CreateNoteAsync(MergeRequestDescriptor, parameters);
         }
         catch (OperatorException)
         {
            throw new DiscussionCreatorException(false);
         }
      }

      async public Task CreateDiscussionAsync(NewDiscussionParameters parameters)
      {
         try
         {
            await DiscussionOperator.CreateDiscussionAsync(MergeRequestDescriptor, parameters);
         }
         catch (OperatorException ex)
         {
            bool handled = await handleGitlabError(parameters, ex);
            throw new DiscussionCreatorException(handled);
         }
      }

      async private Task<bool> handleGitlabError(NewDiscussionParameters parameters, OperatorException ex)
      {
         if (ex.InternalException is GitLabRequestException rex)
         {
            var webException = rex.WebException;
            var response = ((System.Net.HttpWebResponse)webException.Response);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
               // Something went wrong at the GitLab site, let's report a discussion without Position
               return await createMergeRequestWithoutPosition(parameters);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
               // Something went wrong at the GitLab site, let's report a discussion without Position
               await cleanupBadNotes(parameters);
               return await createMergeRequestWithoutPosition(parameters);
            }
         }

         return false;
      }

      async private Task<bool> createMergeRequestWithoutPosition(NewDiscussionParameters parameters)
      {
         Debug.Assert(parameters.Position.HasValue);

         Trace.TraceInformation("Reporting a discussion without Position (fallback)");

         parameters.Body = getFallbackInfo(parameters.Position.Value) + "<br>" + parameters.Body;
         parameters.Position = null;

         try
         {
            await DiscussionOperator.CreateDiscussionAsync(MergeRequestDescriptor, parameters);
         }
         catch (OperatorException ex)
         {
            ExceptionHandlers.Handle(ex, "Cannot create a discussion (again)");
            return false;
         }
         return true;
      }

      private string getFallbackInfo(PositionParameters position)
      {
         return "<b>" + (position.OldPath?.ToString() ?? "N/A") + "</b>"
            + " (line " + (position.OldLine?.ToString() ?? "N/A") + ") <i>vs</i> "
            + "<b>" + (position.NewPath?.ToString() ?? "N/A") + "</b>"
            + " (line " + (position.NewLine?.ToString() ?? "N/A") + ")";
      }

      // Instead of searching for a latest discussion note with some heuristically prepared parameters,
      // let's clean up all similar notes, including a recently added one
      async private Task cleanupBadNotes(NewDiscussionParameters parameters)
      {
         Debug.Assert(parameters.Position.HasValue);

         Trace.TraceInformation("Looking up for a note with bad position...");

         int deletedCount = 0;

         List<Discussion> discussions = await DiscussionOperator.GetDiscussionsAsync(MergeRequestDescriptor);
         if (discussions == null)
         {
            Trace.TraceWarning(String.Format("No discussions found"));
            return;
         }

         foreach (Discussion discussion in discussions)
         {
            foreach (DiscussionNote note in discussion.Notes)
            {
               if (arePositionsEqual(note.Position, parameters.Position.Value))
               {
                  Trace.TraceInformation(
                     "Deleting discussion note." +
                     " Id: {0}, Author.Username: {1}, Created_At: {2} (LocalTime), Body:\n{3}",
                     note.Id.ToString(), note.Author.Username, note.Created_At.ToLocalTime(), note.Body);

                  await DiscussionOperator.DeleteNoteAsync(MergeRequestDescriptor, note.Id);
                  ++deletedCount;
               }
            }
         }

         Trace.TraceInformation(String.Format("Deleted {0} notes", deletedCount));
      }

      /// <summary>
      /// Compares GitLabSharp.Position object which is received from GitLab
      /// to GitLabSharp.PositionParameters whichi is sent to GitLab for equality
      /// </summary>
      /// <returns>true if objects point to the same position</returns>
      private bool arePositionsEqual(Position pos, PositionParameters posParams)
      {
         return pos.Base_SHA == posParams.BaseSHA
             && pos.Head_SHA == posParams.HeadSHA
             && pos.Start_SHA == posParams.StartSHA
             && pos.Old_Line == posParams.OldLine
             && pos.Old_Path == posParams.OldPath
             && pos.New_Line == posParams.NewLine
             && pos.New_Path == posParams.NewPath;
      }

      private DiscussionOperator DiscussionOperator { get; }
      private MergeRequestDescriptor MergeRequestDescriptor { get; }
   }
}

