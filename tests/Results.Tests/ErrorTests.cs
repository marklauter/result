namespace Results.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void Validation_CreatesErrorWithValidationType()
    {
        var error = Error.Validation("err.input", "input is invalid");
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("err.input", error.Code);
        Assert.Equal("input is invalid", error.Message);
    }

    [Fact]
    public void NotFound_CreatesErrorWithNotFoundType()
    {
        var error = Error.NotFound("err.fact.not_found", "fact not found");
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("err.fact.not_found", error.Code);
        Assert.Equal("fact not found", error.Message);
    }

    [Fact]
    public void Gone_CreatesErrorWithGoneType()
    {
        var error = Error.Gone("err.policy.gone", "policy was deleted");
        Assert.Equal(ErrorType.Gone, error.Type);
        Assert.Equal("err.policy.gone", error.Code);
        Assert.Equal("policy was deleted", error.Message);
    }

    [Fact]
    public void Conflict_CreatesErrorWithConflictType()
    {
        var error = Error.Conflict("err.version.conflict", "version mismatch");
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    [Fact]
    public void Undefined_CreatesErrorWithUndefinedType()
    {
        var error = Error.Undefined("err.boom", "something went wrong");
        Assert.Equal(ErrorType.Undefined, error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Factory_Throws_IfCodeIsNullOrWhitespace(string? code) =>
        Assert.ThrowsAny<ArgumentException>(() => Error.NotFound(code!, "message"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Factory_Throws_IfMessageIsNullOrWhitespace(string? message) =>
        Assert.ThrowsAny<ArgumentException>(() => Error.NotFound("err.x", message!));

    [Fact]
    public void Default_IsUndefinedAndThrowsOnCodeAndMessageReads()
    {
        // default(Error) is an uninitialized instance — itself a bug. Type lands in the
        // treated-as-a-bug bucket; Code/Message fail loudly instead of leaking null through
        // their non-nullable declarations (null strings interpolate silently otherwise).
        var error = default(Error);
        Assert.Equal(ErrorType.Undefined, error.Type);
        _ = Assert.Throws<InvalidOperationException>(() => error.Code);
        _ = Assert.Throws<InvalidOperationException>(() => error.Message);

        // The record-synthesized PrintMembers reads Code/Message, so ToString inherits the
        // throw — string interpolation and log formatting of a trash Error fail loudly too.
        _ = Assert.Throws<InvalidOperationException>(error.ToString);
    }

    [Fact]
    public void Undefined_FactoryInstance_ReadsCodeAndMessageNormally()
    {
        // Type == Undefined alone is not the trash discriminator — factory-built
        // Undefined errors are legitimate and fully readable.
        var error = Error.Undefined("err.boom", "something went wrong");
        Assert.Equal("err.boom", error.Code);
        Assert.Equal("something went wrong", error.Message);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForSameTypeAndCodeAndMessage()
    {
        var a = Error.NotFound("err.x", "msg");
        var b = Error.NotFound("err.x", "msg");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenTypeDiffers()
    {
        var a = Error.NotFound("err.x", "msg");
        var b = Error.Gone("err.x", "msg");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenCodeDiffers()
    {
        var a = Error.NotFound("err.x", "msg");
        var b = Error.NotFound("err.y", "msg");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenMessageDiffers()
    {
        // Messages interpolate runtime values, so same type+code with different messages are
        // distinct errors. Any future dedup of accumulated errors inherits this semantic.
        var a = Error.Validation("err.invalid", "'foo' contains invalid characters");
        var b = Error.Validation("err.invalid", "'bar' contains invalid characters");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValue_ForEqualErrors()
    {
        var a = Error.NotFound("err.x", "msg");
        var b = Error.NotFound("err.x", "msg");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
