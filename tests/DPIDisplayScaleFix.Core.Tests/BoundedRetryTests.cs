using DPIDisplayScaleFix.Core;

public sealed class BoundedRetryTests
{
    [Fact]
    public void Retries_retryable_errors_then_returns_the_successful_result()
    {
        int calls = 0;

        int result = BoundedRetry.Execute(
            () => ++calls < 3 ? (0, 122) : (42, 0),
            retryableErrorCode: 122,
            maximumAttempts: 3,
            error => $"failure {error}");

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void Does_not_retry_a_non_retryable_error()
    {
        int calls = 0;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            BoundedRetry.Execute(
                () => { calls++; return (0, 5); },
                retryableErrorCode: 122,
                maximumAttempts: 3,
                error => $"failure {error}"));

        Assert.Equal(1, calls);
        Assert.Equal("failure 5", exception.Message);
    }

    [Fact]
    public void Stops_after_the_configured_number_of_attempts()
    {
        int calls = 0;

        Assert.Throws<InvalidOperationException>(() =>
            BoundedRetry.Execute(
                () => { calls++; return (0, 122); },
                retryableErrorCode: 122,
                maximumAttempts: 2,
                error => $"failure {error}"));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Requires_at_least_one_attempt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BoundedRetry.Execute(
                () => (42, 0),
                retryableErrorCode: 122,
                maximumAttempts: 0,
                error => $"failure {error}"));
    }
}