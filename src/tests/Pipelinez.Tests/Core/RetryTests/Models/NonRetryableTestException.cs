namespace Pipelinez.Tests.Core.RetryTests.Models;

public sealed class NonRetryableTestException : InvalidOperationException
{
    public NonRetryableTestException(string message) : base(message)
    {
    }
}
