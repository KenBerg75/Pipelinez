namespace Pipelinez.Tests.Core.RetryTests.Models;

public sealed class RetryTestException : InvalidOperationException
{
    public RetryTestException(string message) : base(message)
    {
    }
}
