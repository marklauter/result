using System.Diagnostics.CodeAnalysis;

namespace Results;

/// <summary>
/// A named domain failure carrying a typed category, a machine-readable code, and a human-readable message. Every valid instance comes from the static
/// factories. <c>default(Error)</c> is an uninitialized instance, itself a bug, whose <see cref="Code"/> and <see cref="Message"/> reads throw
/// <see cref="InvalidOperationException"/> rather than leaking nulls through their non-nullable declarations.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error is the ubiquitous-language term for a failure value and the type the whole Result surface is written in terms of; renaming it to satisfy Visual Basic's Error statement would cost every caller and every doc comment. VB consumers can escape it as [Error].")]
public readonly record struct Error
{
    /// <summary>The category of failure. Drives the caller's handling logic.</summary>
    public ErrorType Type { get; }

    private const string UninitializedMessage = "uninitialized Error: valid instances come from the Error factories";

    /// <summary>Stable, machine-readable identifier for the specific failure (for example, "fact.not_found").</summary>
    /// <exception cref="InvalidOperationException">The instance is an uninitialized <c>default(Error)</c>.</exception>
    public string Code => field ?? throw new InvalidOperationException(UninitializedMessage);

    /// <summary>Human-readable message suitable for logs and error responses.</summary>
    /// <exception cref="InvalidOperationException">The instance is an uninitialized <c>default(Error)</c>.</exception>
    public string Message => field ?? throw new InvalidOperationException(UninitializedMessage);

    private Error(ErrorType type, string code, string message)
    {
        Type = type;
        Code = code;
        Message = message;
    }

    private static Error Create(ErrorType type, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new Error(type, code, message);
    }

    /// <summary>Constructs a <see cref="ErrorType.Validation"/> error with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Validation"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public static Error Validation(string code, string message) => Create(ErrorType.Validation, code, message);

    /// <summary>Constructs a <see cref="ErrorType.NotFound"/> error with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.NotFound"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public static Error NotFound(string code, string message) => Create(ErrorType.NotFound, code, message);

    /// <summary>Constructs a <see cref="ErrorType.Gone"/> error with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Gone"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public static Error Gone(string code, string message) => Create(ErrorType.Gone, code, message);

    /// <summary>Constructs a <see cref="ErrorType.Conflict"/> error with the given <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Conflict"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.</exception>
    public static Error Conflict(string code, string message) => Create(ErrorType.Conflict, code, message);
}
