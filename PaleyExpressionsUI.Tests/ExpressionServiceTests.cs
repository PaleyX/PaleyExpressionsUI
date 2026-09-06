using PaleyExpressionsUI.ExpressionService;

namespace PaleyExpressionsUI.Tests;

public class ExpressionServiceTests
{
    [Fact]
    public void SimpleSignature()
    {
        var f1 = typeof(ExpressionServiceTests).GetMethod("F1");

        string result = Tools.Signature(f1!);

        Assert.Equal("string s", result);
    }

    [Fact]
    public void SignatureWithFuncParams()
    {
        var f2 = typeof(ExpressionServiceTests).GetMethod("F2");

        string result = Tools.Signature(f2!);

        Assert.Equal("params Func<object>[] f", result);
    }

    public static string? F1(string s) => s;

    public static object? F2(params Func<object?>[] f) => null;
}