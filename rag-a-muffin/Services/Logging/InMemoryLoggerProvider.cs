namespace RagAMuffin.Services.Logging
{
    public sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly InMemoryLogBuffer _buffer;
        public InMemoryLoggerProvider(InMemoryLogBuffer buffer) => _buffer = buffer;
        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _buffer);
        public void Dispose() { }
    }

    internal sealed class InMemoryLogger : ILogger
    {
        private readonly string _category;
        private readonly InMemoryLogBuffer _buffer;

        public InMemoryLogger(string category, InMemoryLogBuffer buffer)
        {
            _category = category;
            _buffer   = buffer;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var msg = formatter(state, exception);
            if (exception is not null) msg += $"\n{exception}";
            _buffer.Add(level.ToString(), _category, msg);
        }
    }
}
