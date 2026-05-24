namespace BookOfEternityClient.Core;

public interface IConsoleInputSource
{
    bool IsScripted { get; }
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept = true);
    string? ReadLine();
    void AssertCompleted();
}

public sealed class SystemConsoleInputSource : IConsoleInputSource
{
    public static SystemConsoleInputSource Instance { get; } = new();

    private SystemConsoleInputSource()
    {
    }

    public bool IsScripted => false;

    public bool KeyAvailable => Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey(bool intercept = true) => Console.ReadKey(intercept);

    public string? ReadLine() => Console.ReadLine();

    public void AssertCompleted()
    {
        // Interactive input has no finite script to validate.
    }
}
