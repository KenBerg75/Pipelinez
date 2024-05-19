using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pipelinez.Tests.Core.LoggingTests.Models;

public class TestLogFactory : ILoggerFactory
{
    public int LoggersCreated { get; private set; } = 0;
    
    public ILogger CreateLogger(string categoryName)
    {
        LoggersCreated += 1;
        return NullLogger.Instance;
    }

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotImplementedException();
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}