using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Serilog;

namespace Natoshare.Api.Jobs;

// Registered once as a global Hangfire filter, so every recurring job in the app
// automatically gets one thing "the jobs are solid" needs: a loud log line the
// moment one fails, instead of a failure only ever showing up if someone happens to
// open the Hangfire dashboard and notice it sitting there.
public class HangfireFailureAlertFilter : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is FailedState failedState)
        {
            Log.Error(
                failedState.Exception,
                "Hangfire job {JobId} ({Job}) failed: {Reason}",
                context.BackgroundJob.Id, context.BackgroundJob.Job, failedState.Reason);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
