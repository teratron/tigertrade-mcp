using TigerTradeMcp.Core;

namespace TigerTradeMcp.Tests;

public sealed class SafeJsonTests
{
    [Fact]
    public void Serialize_UseCamelCase()
    {
        var result = SafeJson.Serialize(new { MyProperty = "value" });
        result.Should().Contain("\"myProperty\"");
    }

    [Fact]
    public void Serialize_OmitsNullProperties()
    {
        var result = SafeJson.Serialize(new { Present = "yes", Absent = (string?)null });
        result.Should().NotContain("absent");
        result.Should().Contain("\"present\"");
    }

    [Fact]
    public void Serialize_ProducesCompactOutput()
    {
        var result = SafeJson.Serialize(new { a = 1 });
        result.Should().NotContain("\n");
        result.Should().NotContain("  ");
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new TestDto("hello", 42);
        var json = SafeJson.Serialize(original);
        var deserialized = SafeJson.Deserialize<TestDto>(json);
        deserialized.Should().BeEquivalentTo(original);
    }

    private sealed record TestDto(string Name, int Value);
}
