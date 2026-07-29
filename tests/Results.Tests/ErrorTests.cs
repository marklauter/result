namespace Results.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void Validation_CreatesErrorWithValidationType()
    {
        var error = Error.Validation(ErrorCode.Unchecked("err.input"), ErrorMessage.Unchecked("input is invalid"));
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("err.input", error.Code.Value);
        Assert.Equal("input is invalid", error.Message.Value);
    }

    [Fact]
    public void NotFound_CreatesErrorWithNotFoundType()
    {
        var error = Error.NotFound(ErrorCode.Unchecked("err.fact.not_found"), ErrorMessage.Unchecked("fact not found"));
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("err.fact.not_found", error.Code.Value);
        Assert.Equal("fact not found", error.Message.Value);
    }

    [Fact]
    public void Gone_CreatesErrorWithGoneType()
    {
        var error = Error.Gone(ErrorCode.Unchecked("err.policy.gone"), ErrorMessage.Unchecked("policy was deleted"));
        Assert.Equal(ErrorType.Gone, error.Type);
        Assert.Equal("err.policy.gone", error.Code.Value);
        Assert.Equal("policy was deleted", error.Message.Value);
    }

    [Fact]
    public void Conflict_CreatesErrorWithConflictType()
    {
        var error = Error.Conflict(ErrorCode.Unchecked("err.version.conflict"), ErrorMessage.Unchecked("version mismatch"));
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Fact]
    public void Default_IsUndefinedAndThrowsOnCodeAndMessageReads()
    {
        // default(Error) is an uninitialized instance — itself a bug. Type lands in the
        // treated-as-a-bug bucket; Code/Message fail loudly instead of leaking null-carrying
        // default wrappers (a null Value interpolates silently otherwise). Undefined is
        // unconstructible except as default(Error), so the Type guard is the detector.
        var error = default(Error);
        Assert.Equal(ErrorType.Undefined, error.Type);
        _ = Assert.Throws<InvalidOperationException>(() => error.Code);
        _ = Assert.Throws<InvalidOperationException>(() => error.Message);

        // The record-synthesized PrintMembers reads Code/Message, so ToString inherits the
        // throw — string interpolation and log formatting of a trash Error fail loudly too.
        _ = Assert.Throws<InvalidOperationException>(error.ToString);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForSameTypeAndCodeAndMessage()
    {
        var a = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var b = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenTypeDiffers()
    {
        var a = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var b = Error.Gone(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenCodeDiffers()
    {
        var a = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var b = Error.NotFound(ErrorCode.Unchecked("err.y"), ErrorMessage.Unchecked("msg"));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenMessageDiffers()
    {
        // Messages interpolate runtime values, so same type+code with different messages are
        // distinct errors. Any future dedup of accumulated errors inherits this semantic.
        var a = Error.Validation(ErrorCode.Unchecked("err.invalid"), ErrorMessage.Unchecked("'foo' contains invalid characters"));
        var b = Error.Validation(ErrorCode.Unchecked("err.invalid"), ErrorMessage.Unchecked("'bar' contains invalid characters"));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForEqualErrors()
    {
        var a = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        var b = Error.NotFound(ErrorCode.Unchecked("err.x"), ErrorMessage.Unchecked("msg"));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
