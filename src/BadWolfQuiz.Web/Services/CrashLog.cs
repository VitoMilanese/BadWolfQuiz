namespace BadWolfQuiz.Web.Services;

public sealed class CrashLog(IWebHostEnvironment environment)
{
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "crash.log");

    public string FilePath => _path;

    public void Write(string source, Exception? exception = null)
    {
        try
        {
            var entry = $"[{DateTimeOffset.UtcNow:O}] {source}{Environment.NewLine}";
            if (exception is not null)
            {
                entry += exception + Environment.NewLine;
            }

            entry += Environment.NewLine;

            lock (_gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, entry);
            }
        }
        catch
        {
            // Diagnostics must never become another reason for the process to stop.
        }
    }
}
