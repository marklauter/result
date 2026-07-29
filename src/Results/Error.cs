using System.Diagnostics.CodeAnalysis;

namespace Results;

/// <summary>
/// A named domain failure carrying a typed category, a machine-readable <see cref="ErrorCode"/>, and a human-readable <see cref="ErrorMessage"/>. Every
/// valid instance comes from the static factories, which take only the typed wrappers — no <see cref="string"/> crosses this surface, so lifting a
/// primitive is the caller's act, through <c>Checked</c> or <c>Unchecked</c> on the wrapper that names its meaning. <c>default(Error)</c> is an
/// uninitialized instance, itself a bug, whose <see cref="Code"/> and <see cref="Message"/> reads throw <see cref="InvalidOperationException"/> rather
/// than leaking null-carrying wrappers through their declarations.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error is the ubiquitous-language term for a failure value and the type the whole Result surface is written in terms of; renaming it to satisfy Visual Basic's Error statement would cost every caller and every doc comment. VB consumers can escape it as [Error].")]
public readonly record struct Error
{
    /// <summary>The category of failure. Drives the caller's handling logic.</summary>
    public ErrorType Type { get; }

    private const string UninitializedMessage = "uninitialized Error: valid instances come from the Error factories";

    // ErrorType.Undefined is unconstructible except as default(Error) — no factory emits it — so the Type check below is exactly the
    // uninitialized-instance detector the wrappers' non-nullable structs can no longer provide through a field-null check.

    /// <summary>Stable, machine-readable identifier for the specific failure (for example, "fact.not_found").</summary>
    /// <exception cref="InvalidOperationException">The instance is an uninitialized <c>default(Error)</c>.</exception>
    public ErrorCode Code =>
        Type != ErrorType.Undefined
            ? field
            : throw new InvalidOperationException(UninitializedMessage);

    /// <summary>Human-readable message suitable for logs and error responses.</summary>
    /// <exception cref="InvalidOperationException">The instance is an uninitialized <c>default(Error)</c>.</exception>
    public ErrorMessage Message =>
        Type != ErrorType.Undefined
            ? field
            : throw new InvalidOperationException(UninitializedMessage);

    private Error(ErrorType type, ErrorCode code, ErrorMessage message)
    {
        Type = type;
        Code = code;
        Message = message;
    }

    /// <summary>Constructs a <see cref="ErrorType.Validation"/> error carrying <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Validation"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    public static Error Validation(ErrorCode code, ErrorMessage message)
        => new(ErrorType.Validation, code, message);

    /// <summary>Constructs a <see cref="ErrorType.NotFound"/> error carrying <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.NotFound"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    public static Error NotFound(ErrorCode code, ErrorMessage message)
        => new(ErrorType.NotFound, code, message);

    /// <summary>Constructs a <see cref="ErrorType.Gone"/> error carrying <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Gone"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    public static Error Gone(ErrorCode code, ErrorMessage message)
        => new(ErrorType.Gone, code, message);

    /// <summary>Constructs a <see cref="ErrorType.Conflict"/> error carrying <paramref name="code"/> and <paramref name="message"/>.</summary>
    /// <returns>A <see cref="ErrorType.Conflict"/> <see cref="Error"/> carrying <paramref name="code"/> and <paramref name="message"/>.</returns>
    public static Error Conflict(ErrorCode code, ErrorMessage message) => new(ErrorType.Conflict, code, message);
}
