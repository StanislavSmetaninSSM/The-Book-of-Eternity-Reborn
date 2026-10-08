using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public enum DesktopOpenStatus { Requested, Missing, Unsupported, Unavailable, Failed, Cancelled, Suppressed }

/// <summary>Requested means the managed association call returned, never proof of display.</summary>
public sealed record DesktopOpenResult(DesktopOpenStatus Status, string Path, Exception? Error = null)
{
    public string Message => Status switch
    {
        DesktopOpenStatus.Requested => "Запрос открытия передан системе. Если окно не появилось, откройте путь вручную.",
        DesktopOpenStatus.Missing => "Файл или папка не найдены. Проверьте путь для ручного открытия.",
        DesktopOpenStatus.Unsupported => "Этот файл или раздел не поддерживает автоматическое открытие. Используйте путь вручную.",
        DesktopOpenStatus.Unavailable => "Не удалось передать запрос открытия системе. Возможно, нет подходящего приложения или рабочего стола. Используйте путь вручную.",
        DesktopOpenStatus.Failed => "Не удалось подготовить путь или отправить запрос открытия. Используйте путь вручную.",
        DesktopOpenStatus.Cancelled => "Открытие отменено. При необходимости используйте путь вручную.",
        _ => ""
    };
    public string ToMarkup() => $"[dim]{Markup.Escape(Message)}[/]\n[yellow]{Markup.Escape(Path)}[/]";
}

/// <summary>One managed association request, with a controlled launch boundary and no shell or replay.</summary>
public sealed class DesktopPathOpener
{
    private readonly Action<ProcessStartInfo> _launch;
    public DesktopPathOpener(Action<ProcessStartInfo>? launch = null)
        => _launch = launch ?? (start => { using var process = Process.Start(start); });

    public DesktopOpenResult OpenFile(string path) => Request(path, directory: false, createIfMissing: false);
    public DesktopOpenResult OpenFolder(string path, bool createIfMissing = true) => Request(path, directory: true, createIfMissing);

    private DesktopOpenResult Request(string path, bool directory, bool createIfMissing)
    {
        try
        {
            if (directory && createIfMissing) Directory.CreateDirectory(path);
            if (!(directory ? Directory.Exists(path) : File.Exists(path))) return new(DesktopOpenStatus.Missing, path);
            _launch(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            // Some Windows associations succeed without returning a Process. Do not invent display evidence.
            return new(DesktopOpenStatus.Requested, path);
        }
        catch (OperationCanceledException ex) { return new(DesktopOpenStatus.Cancelled, path, ex); }
        catch (Win32Exception ex) { return new(DesktopOpenStatus.Unavailable, path, ex); }
        catch (Exception ex) { return new(DesktopOpenStatus.Failed, path, ex); }
    }
}
