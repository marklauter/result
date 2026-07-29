namespace Results.Tests;

public sealed class ErrorCodeTests
{
    [Fact]
    public void Checked_ValidString_ReturnsSuccessCarryingValue()
    {
        var success = Assert.IsType<Result<ErrorCode>.Success>(ErrorCode.Checked("err.fact.not_found"));
        Assert.Equal("err.fact.not_found", success.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Checked_NullOrWhitespace_ReturnsValidationFailure(string? value)
    {
        var failure = Assert.IsType<Result<ErrorCode>.Failure>(ErrorCode.Checked(value));
        var error = Assert.Single(failure.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("err.error_code.invalid", error.Code.Value);
    }

    [Fact]
    public void Unchecked_PassesValueThroughUnvalidated() =>
        Assert.Equal("anything goes", ErrorCode.Unchecked("anything goes").Value);

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(ErrorCode.Unchecked("err.x"), ErrorCode.Unchecked("err.x"));
        Assert.NotEqual(ErrorCode.Unchecked("err.x"), ErrorCode.Unchecked("err.y"));
        Assert.True(ErrorCode.Unchecked("err.x") == ErrorCode.Unchecked("err.x"));
        Assert.True(ErrorCode.Unchecked("err.x") != ErrorCode.Unchecked("err.y"));
    }

    [Fact]
    public void CompareTo_ComparesOrdinallyOverValue()
    {
        Assert.True(ErrorCode.Unchecked("err.a").CompareTo(ErrorCode.Unchecked("err.b")) < 0);
        Assert.Equal(0, ErrorCode.Unchecked("err.a").CompareTo(ErrorCode.Unchecked("err.a")));
        Assert.True(ErrorCode.Unchecked("err.b").CompareTo(ErrorCode.Unchecked("err.a")) > 0);

        // Ordinal, not culture-aware: 'B' (66) sorts before 'a' (97).
        Assert.True(ErrorCode.Unchecked("B").CompareTo(ErrorCode.Unchecked("a")) < 0);
    }

    [Fact]
    public void ComparisonOperators_OrderOrdinallyByValue()
    {
        var lesser = ErrorCode.Unchecked("err.a");
        var greater = ErrorCode.Unchecked("err.b");

        Assert.True(lesser < greater);
        Assert.True(lesser <= greater);
        Assert.True(greater > lesser);
        Assert.True(greater >= lesser);

        Assert.False(greater < lesser);
        Assert.False(greater <= lesser);
        Assert.False(lesser > greater);
        Assert.False(lesser >= greater);

        Assert.True(lesser <= ErrorCode.Unchecked("err.a"));
        Assert.True(lesser >= ErrorCode.Unchecked("err.a"));
    }

    [Fact]
    public void ToString_ReturnsValue() =>
        Assert.Equal("err.x", ErrorCode.Unchecked("err.x").ToString());
}
