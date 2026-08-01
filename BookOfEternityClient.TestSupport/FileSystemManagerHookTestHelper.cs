using System.Reflection;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

internal static class FileSystemManagerHookTestHelper
{
    internal static FileSystemManagerHooks WithPathHook(
        string propertyName,
        Func<string, Task> callback)
    {
        var hooks = new FileSystemManagerHooks();
        SetPathHook(hooks, propertyName, callback);
        return hooks;
    }

    internal static void SetPathHook(
        FileSystemManagerHooks hooks,
        string propertyName,
        Func<string, Task> callback)
    {
        GetRequiredProperty(propertyName).SetValue(hooks, callback);
    }

    internal static FileSystemManagerHooks WithBooleanOverride(
        string propertyName,
        bool value)
    {
        var hooks = new FileSystemManagerHooks();
        SetBooleanOverride(hooks, propertyName, value);
        return hooks;
    }

    internal static void SetBooleanOverride(
        FileSystemManagerHooks hooks,
        string propertyName,
        bool value)
    {
        GetRequiredProperty(propertyName).SetValue(hooks, value);
    }

    private static PropertyInfo GetRequiredProperty(string propertyName) =>
        typeof(FileSystemManagerHooks).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            $"FileSystemManagerHooks property was not found: {propertyName}");
}
