namespace SNIBypassGUI.Utils.Results
{
    /// <summary>
    /// Represents the result of an operation, which can either be a success or a failure.
    /// This is an immutable, readonly record struct.
    /// </summary>
    /// <typeparam name="T">The type of the successful result value.</typeparam>
    public readonly record struct ParseResult<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the successful result of the operation.
        /// Returns default(T) if the operation failed.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets the message describing the error.
        /// Returns null if the operation was successful.
        /// </summary>
        public string ErrorMessage { get; }

        // Private constructor forces the use of static factory methods.
        private ParseResult(bool isSuccess, T value, string errorMessage)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ParseResult{T}"/> representing a successful operation.
        /// </summary>
        /// <param name="value">The successful result value.</param>
        public static ParseResult<T> Success(T value) =>
            new(true, value, null);

        /// <summary>
        /// Creates a new instance of <see cref="ParseResult{T}"/> representing a failed operation.
        /// </summary>
        /// <param name="errorMessage">The message describing the error.</param>
        public static ParseResult<T> Failure(string errorMessage) =>
            new(false, default, errorMessage);
    }
}
