using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pipelinez.Core.Logging;
using Microsoft.Extensions.Logging;


/// <summary>
/// Manages logging components that are used within Pipelinez
/// </summary>
internal class LoggingManager : ILoggerFactory
{
    #region Singleton
    
    private static LoggingManager _instance;
    private static object syncRoot = new Object();
    private ILoggerFactory _logFactory = new NullLoggerFactory();

    private LoggingManager()
    {
    }

    /// <summary>
    /// Build LoggingManager
    /// </summary>
    public static LoggingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (syncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new LoggingManager();
                    }
                }
            }

            return _instance;
        }
    }
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Assigns a logger factory that can be used to create loggers in Pipelinez
    /// </summary>
    /// <param name="logFactory"></param>
    internal void AssignLogFactory(ILoggerFactory logFactory)
    {
        Guard.Against.Null(logFactory, nameof(logFactory), "Cannot provide a null LogFactory. If you do not want logging, don't call .WithLogging()");
        
        _logFactory = logFactory;
    }
    
    #endregion

    #region ILoggerFactory Implementation
    
    public ILogger CreateLogger(string categoryName)
    {
        return _logFactory.CreateLogger(categoryName);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        _logFactory.AddProvider(provider);
    }

    public void Dispose()
    {
        _logFactory?.Dispose();
    }
    
    #endregion
}