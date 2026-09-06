using PaleyExpressionsUI.ExpressionService;

namespace PaleyExpressionsUI.Tests;

public class UtilsTests
{
    private const string expressionJson = """
        {
            "Expression": "x+y",
            "Variables": {
                "x": 42,
                "y": 3.14,
                "s": "hello",
                "t": true,
                "f": false
            }
        }
        """;

    [Fact]
    public void ConvertJsonElement_ParsesVariousKinds()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(expressionJson);
        var root = doc.RootElement;
        var conv = Utils.ConvertJsonElement(root);

        var dict = Assert.IsType<Dictionary<string, object?>>(conv);
        Assert.IsType<string>(dict["Expression"]);

        var variables = Assert.IsType<Dictionary<string, object?>>(dict["Variables"]);
        Assert.IsType<double>(variables["x"]);
        Assert.IsType<double>(variables["y"]);
        Assert.IsType<string>(variables["s"]);
        Assert.IsType<bool>(variables["t"]);
        Assert.IsType<bool>(variables["f"]);
    }
}
