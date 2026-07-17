using Xunit;

namespace TestMap_Example.Tests;

public sealed class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSum()
    {
        var calculator = new Calculator();

        var result = calculator.Add(2, 3);

        Assert.Equal(5, result);
    }
}
