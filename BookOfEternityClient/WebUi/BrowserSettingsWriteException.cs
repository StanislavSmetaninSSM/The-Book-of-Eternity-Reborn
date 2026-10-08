namespace BookOfEternityClient.WebUi;

public sealed class BrowserSettingsWriteException : InvalidOperationException
{
    internal BrowserSettingsWriteException(BrowserPreparedWriteDisposition disposition, string message) : base(message) =>
        Disposition = disposition;

    public BrowserPreparedWriteDisposition Disposition { get; }
}
