using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityClient.Core;

// A completed decision survives transport confirmation failure. This carrier
// blocks continuation; its close description never creates authority or replay.
internal interface IMainOperationContinuationFailure
{
    MainOperationOutcome EstablishedOutcome { get; }
}
internal sealed class MainOperationContinuationException<T> : IOException, IMainOperationContinuationFailure
{
    internal T EstablishedResult { get; }
    public MainOperationOutcome EstablishedOutcome { get; }
    internal MainOperationClose? OriginalClose { get; }
    internal MainOperationContinuationException(T result,MainOperationOutcome outcome,MainOperationClose? close,Exception failure)
        : base("The original operation established a result, but continuation is unconfirmed; do not replay.",failure)
    {
        EstablishedResult=result;EstablishedOutcome=outcome;OriginalClose=close;
        Data["EstablishedOperationResult"]=result;
        Data["EstablishedOperationOutcome"]=outcome;
        Data["OriginalOperationClose"]=close;
    }
}
