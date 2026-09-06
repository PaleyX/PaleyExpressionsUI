using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;
using PaleyExpressionsUI.ExpressionService;

namespace PaleyExpressionsUI.Tests;

public class CompilationCacheTests
{
    private readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger("CompilationTests");

    [Fact]
    public async Task CompileCode_CachesAndAllowsUnload()
    {
        const string source = @"public static class Functions { public static int Add(int a,int b) => a + b; }";

        var asm1 = await CompilationCache.CompileCodeAsync(source, _logger);
        Assert.NotNull(asm1);

        var asm2 = await CompilationCache.CompileCodeAsync(source, _logger);
        Assert.Same(asm1, asm2);

        // compute key the same way as the cache (hex sha256)
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(source ?? string.Empty));
        var key = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        var unloaded = CompilationCache.UnloadCachedAssembly(key, _logger);
        Assert.True(unloaded);

        var asm3 = await CompilationCache.CompileCodeAsync(source, _logger);
        Assert.NotNull(asm3);
        // After unload and recompile, Assembly instance should be different
        Assert.NotSame(asm1, asm3);
    }

    [Fact]
    public async Task CompileCode_ConcurrentRequests_ReturnSameAssembly()
    {
        const string source = @"public static class Functions { public static int Add(int a,int b) => a + b; }";

        var tasks = Enumerable.Range(0, 8).Select(_ => CompilationCache.CompileCodeAsync(source, _logger)).ToArray();
        var results = await Task.WhenAll(tasks);

        foreach (var r in results)
        {
            Assert.NotNull(r);
            Assert.Same(results[0], r);
        }
    }
}
