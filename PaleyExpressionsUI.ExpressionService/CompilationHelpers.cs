using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using PaleyExpressions.Runners;

namespace PaleyExpressionsUI.ExpressionService;

internal static class CompilationCache
{
    private static readonly ConcurrentDictionary<string, Task<Assembly?>> _compileCache = new();
    private static readonly ConcurrentDictionary<string, AssemblyLoadContext> _loadContexts = new();

    public static Task<Assembly?> CompileCodeAsync(string sourceCode, ILogger logger)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sourceCode ?? string.Empty));
        var key = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var shortKey = key.Length > 8 ? key[..8] : key;

        if (_compileCache.TryGetValue(key, out var existingTask))
        {
            // Log cache hit for the incoming source code
            logger.LogInformation("Compilation cache hit for key {Key}. Returning cached task. Completed: {IsCompleted}", shortKey, existingTask.IsCompleted);
            return existingTask;
        }

        var task = _compileCache.GetOrAdd(key, _ =>
        {
            logger.LogInformation("Compilation cache miss for key {Key}. Queuing compilation.", shortKey);
            var t = CompileAndMaybeCacheRemoveAsync(sourceCode, logger, key);

            t.ContinueWith(ct =>
            {
                if (ct.Status == TaskStatus.RanToCompletion && ct.Result != null)
                {
                    logger.LogInformation("Compilation succeeded for key {Key}, assembly: {Assembly}", shortKey, ct.Result.FullName);
                }
                else if (ct.Status == TaskStatus.RanToCompletion && ct.Result == null)
                {
                    logger.LogWarning("Compilation completed but returned no assembly for key {Key}", shortKey);
                }
                else if (ct.IsFaulted)
                {
                    logger.LogError(ct.Exception, "Compilation task failed for key {Key}", shortKey);
                    _compileCache.TryRemove(key, out var _assemblyOnFault);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return t;
        });

        if (task.IsCompleted)
        {
            logger.LogDebug("Compilation task already completed for key {Key}", shortKey);
        }

        return task;
    }

    private static async Task<Assembly?> CompileAndMaybeCacheRemoveAsync(string sourceCode, ILogger logger, string key)
    {
        var shortKey = key.Length > 8 ? key[..8] : key;
        logger.LogInformation("Starting compilation for key {Key}", shortKey);
        var sw = Stopwatch.StartNew();
        try
        {
            var asm = await CompileCodeInternalAsync(sourceCode, logger, key).ConfigureAwait(false);
            if (asm is null)
            {
                logger.LogWarning("Compilation failed for key {Key}", shortKey);
                _compileCache.TryRemove(key, out var _removed);
            }
            else
            {
                logger.LogInformation("Compilation produced assembly {Assembly} for key {Key} (took {Elapsed}ms)", asm.FullName, shortKey, sw.ElapsedMilliseconds);
            }
            return asm;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Compilation threw an exception for key {Key}", shortKey);
            _compileCache.TryRemove(key, out var _);
            throw;
        }
    }

    private static async Task<Assembly?> CompileCodeInternalAsync(string sourceCode, ILogger logger, string key)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var loadedAssemblyPaths = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct()
            .ToList();

        var references = new List<MetadataReference>(loadedAssemblyPaths.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in loadedAssemblyPaths)
        {
            if (seen.Add(p))
            {
                try { references.Add(MetadataReference.CreateFromFile(p)); } catch { }
            }
        }

        try
        {
            var paley = typeof(ExpressionRunner).Assembly.Location;
            if (!string.IsNullOrEmpty(paley) && seen.Add(paley))
            {
                references.Add(MetadataReference.CreateFromFile(paley));
            }
        }
        catch(Exception ex)
        {
            logger.LogDebug(ex, "Failed to locate PaleyExpressions assembly for compilation references.");
        }

        var assemblyName = "DynamicAssembly_" + (key.Length > 8 ? key.Substring(0, 8) : key);
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithDeterministic(true)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                var location = diagnostic.Location.IsInSource
                    ? diagnostic.Location.GetLineSpan().ToString()
                    : "(no source)";

                var message = $"{diagnostic.Id}: {diagnostic.GetMessage()} ({location})";
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Error:
                        logger.LogError("{Diagnostic}", message);
                        break;
                    case DiagnosticSeverity.Warning:
                        logger.LogWarning("{Diagnostic}", message);
                        break;
                    case DiagnosticSeverity.Info:
                        logger.LogInformation("{Diagnostic}", message);
                        break;
                    default:
                        logger.LogDebug("{Diagnostic}", message);
                        break;
                }
            }
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);

        try
        {
            var alcName = assemblyName + "_ALC";
            var alc = new AssemblyLoadContext(alcName, isCollectible: true);

            using var loadStream = new MemoryStream(ms.ToArray());
            var assembly = alc.LoadFromStream(loadStream);

            _loadContexts[key] = alc;

            return assembly;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load compiled assembly into collectible context");
            return null;
        }
    }

    public static bool UnloadCachedAssembly(string key, ILogger logger)
    {
        if (_compileCache.TryRemove(key, out _))
        {
            if (_loadContexts.TryRemove(key, out var alc))
            {
                try
                {
                    alc.Unload();
                    logger.LogInformation("Requested unload for compiled assembly key {Key}", key.Length > 8 ? key.Substring(0, 8) : key);
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while unloading assembly for key {Key}", key.Length > 8 ? key.Substring(0, 8) : key);
                }
            }
            return true;
        }

        return false;
    }
}

internal static class Utils
{
    public static object? ConvertJsonElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                if (el.TryGetDouble(out var d)) return d;
                //TODO: only doubles accepted so this isn't needed and should be a throw
                try { return el.GetDecimal(); } catch { return el.GetDouble(); }

            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in el.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElement(prop.Value);
                }
                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in el.EnumerateArray()) list.Add(ConvertJsonElement(item));
                return list;
            default:
                return el.ToString();
        }
    }
}

public record ExpressionRequest(
    string Expression,
    Dictionary<string, JsonElement>? Variables,
    string? Code
);