using System.Collections.Concurrent;

namespace RagAMuffin.Services.Logging
{
    public record LogEntry(string Id, DateTime At, string Level, string Category, string Message);

    public class InMemoryLogBuffer
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private const int MaxEntries = 500;
        private int _counter;

        public void Add(string level, string category, string message)
        {
            var id = Interlocked.Increment(ref _counter).ToString();
            _entries.Enqueue(new LogEntry(id, DateTime.UtcNow, level, ShortName(category), message));
            while (_entries.Count > MaxEntries)
                _entries.TryDequeue(out _);
        }

        public IReadOnlyList<LogEntry> GetAll() => _entries.ToArray();

        private static string ShortName(string category)
        {
            var dot = category.LastIndexOf('.');
            return dot >= 0 ? category[(dot + 1)..] : category;
        }
    }
}
