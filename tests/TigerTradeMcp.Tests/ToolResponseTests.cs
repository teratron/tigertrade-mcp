using TigerTradeMcp.Core;

namespace TigerTradeMcp.Tests;

public sealed class ToolResponseTests
{
    [Fact]
    public void Success_SetsOkTrue()
    {
        var response = ToolResponse.Success(new { x = 1 });
        response.Ok.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public void Failure_SetsOkFalse()
    {
        var response = ToolResponse.Failure("something went wrong");
        response.Ok.Should().BeFalse();
        response.Error.Should().Be("something went wrong");
        response.Data.Should().BeNull();
    }

    [Fact]
    public void ToJson_SuccessContainsOkTrue()
    {
        var json = ToolResponse.Success(new { value = 42 }).ToJson();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("\"value\":42");
    }

    [Fact]
    public void ToJson_FailureContainsError()
    {
        var json = ToolResponse.Failure("oops").ToJson();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("\"error\":\"oops\"");
    }
}
