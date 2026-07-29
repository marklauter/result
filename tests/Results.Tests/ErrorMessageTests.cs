namespace Results.Tests;

public sealed class ErrorMessageTests
{
    [Fact]
    public void Checked_ValidString_ReturnsSuccessCarryingValue()
    {
        var success = Assert.IsType<Result<ErrorMessage>.Success>(ErrorMessage.Checked("fact not found"));
        Assert.Equal("fact not found", success.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Checked_NullOrWhitespace_ReturnsValidationFailure(string? value)
    {
        var failure = Assert.IsType<Result<ErrorMessage>.Failure>(ErrorMessage.Checked(value));
        var error = Assert.Single(failure.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("err.error_message.invalid", error.Code.Value);
    }

    [Fact]
    public void Unchecked_PassesValueThroughUnvalidated() =>
        Assert.Equal("anything goes", ErrorMessage.Unchecked("anything goes").Value);

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(ErrorMessage.Unchecked("msg"), ErrorMessage.Unchecked("msg"));
        Assert.NotEqual(ErrorMessage.Unchecked("msg"), ErrorMessage.Unchecked("other"));
        Assert.True(ErrorMessage.Unchecked("msg") == ErrorMessage.Unchecked("msg"));
        Assert.True(ErrorMessage.Unchecked("msg") != ErrorMessage.Unchecked("other"));
    }

    [Fact]
    public void CompareTo_ComparesOrdinallyOverValue()
    {
        Assert.True(ErrorMessage.Unchecked("alpha").CompareTo(ErrorMessage.Unchecked("beta")) < 0);
        Assert.Equal(0, ErrorMessage.Unchecked("alpha").CompareTo(ErrorMessage.Unchecked("alpha")));
        Assert.True(ErrorMessage.Unchecked("beta").CompareTo(ErrorMessage.Unchecked("alpha")) > 0);

        // Ordinal, not culture-aware: 'B' (66) sorts before 'a' (97).
        Assert.True(ErrorMessage.Unchecked("B").CompareTo(ErrorMessage.Unchecked("a")) < 0);
    }

    [Fact]
    public void ComparisonOperators_OrderOrdinallyByValue()
    {
        var lesser = ErrorMessage.Unchecked("alpha");
        var greater = ErrorMessage.Unchecked("beta");

        Assert.True(lesser < greater);
        Assert.True(lesser <= greater);
        Assert.True(greater > lesser);
        Assert.True(greater >= lesser);

        Assert.False(greater < lesser);
        Assert.False(greater <= lesser);
        Assert.False(lesser > greater);
        Assert.False(lesser >= greater);

        Assert.True(lesser <= ErrorMessage.Unchecked("alpha"));
        Assert.True(lesser >= ErrorMessage.Unchecked("alpha"));
    }

    [Fact]
    public void ToString_ReturnsValue() =>
        Assert.Equal("fact not found", ErrorMessage.Unchecked("fact not found").ToString());
}
