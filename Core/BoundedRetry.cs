namespace DPIDisplayScaleFix.Core;

public static class BoundedRetry
{
    public static TResult Execute<TResult>(
        Func<(TResult Value, int ErrorCode)> operation,
        int retryableErrorCode,
        int maximumAttempts,
        Func<int, string> errorMessage)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(errorMessage);
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            (TResult value, int errorCode) = operation();
            if (errorCode == 0)
                return value;

            if (errorCode != retryableErrorCode || attempt == maximumAttempts)
                throw new InvalidOperationException(errorMessage(errorCode));
        }

        throw new InvalidOperationException("El reintento terminó en un estado inesperado.");
    }
}