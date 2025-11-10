using System.Collections.Concurrent;
using System.Text;

namespace ServerCore;

public sealed class DailyLogger : IDisposable
{
    private readonly string logDirectory;
    private readonly object fileLock = new();
    private StreamWriter? currentWriter;
    private DateOnly currentDate;
    private bool disposed;

    public DailyLogger(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        currentDate = DateOnly.FromDateTime(DateTime.Now);
        currentWriter = OpenWriterForDate(currentDate);
    }

    private StreamWriter OpenWriterForDate(DateOnly date)
    {
        string filePath = Path.Combine(logDirectory, $"{date:yyyy-MM-dd}.log");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        return writer;
    }

    private void RotateIfNeeded(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (today != currentDate)
        {
            lock (fileLock)
            {
                if (today != currentDate)
                {
                    currentWriter?.Dispose();
                    currentDate = today;
                    currentWriter = OpenWriterForDate(today);
                }
            }
        }
    }

    public void Log(string message)
    {
        if (disposed) return;
        DateTime now = DateTime.Now;
        RotateIfNeeded(now);
        string line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        lock (fileLock)
        {
            currentWriter?.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (fileLock)
        {
            currentWriter?.Dispose();
            currentWriter = null;
        }
    }
}




