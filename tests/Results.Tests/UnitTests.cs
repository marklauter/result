namespace Results.Tests;

public sealed class UnitTests
{
    [Fact]
    public void Value_ReturnsSingleton() => Assert.Equal(Unit.Value, Unit.Value);

    [Fact]
    public void Default_EqualsValue() => Assert.Equal(Unit.Value, default);
}
