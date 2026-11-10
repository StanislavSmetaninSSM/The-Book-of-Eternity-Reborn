namespace BookOfEternityClient.Services;

/// <summary>
/// Distinguishes a stored result, including its legitimate default value, from a noncomputing cache miss.
/// </summary>
/// <typeparam name="TResult">
/// Stored computation value, which may allow a null or default result.
/// </typeparam>
/// <param name="Found">
/// Whether the existing service supplied an eligible detached result.
/// </param>
/// <param name="Value">
/// Detached stored value when found; otherwise an unspecified default that must not be consumed.
/// </param>
internal readonly record struct OperationLookup<TResult>(bool Found, TResult Value);
