using PaleyExpressions;
using PaleyExpressions.Runners;
using PaleyExpressionsUI.ExpressionService;
using System.Linq.Expressions;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "Expression service is running.")
   .WithName("Root");

app.MapGet("/builtins", (HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                                   .CreateLogger("ExpressionService");
    logger.LogInformation("Received builtins request");

    try
    {
        var methods = typeof(Builtins)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(FunctionAttribute), false).Length > 0)
            .ToList();

        var builtins = new List<BuiltinDetails>();

        foreach(var method in methods)
        {
            builtins.Add(new BuiltinDetails(method.Name,
                                            Tools.GetReturnTypeName(method),  
                                            Tools.Signature(method)));
        }

        return Results.Ok(builtins);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve builtins");
        return Results.Problem("Failed to retrieve builtins: " + ex.Message);
    }
})
.WithName("GetBuiltins");

app.MapPost("/expression", async (ExpressionRequest req, HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                                   .CreateLogger("ExpressionService");

    logger.LogInformation("Received expression request: {Json}",
        JsonSerializer.Serialize(req));

    if (!string.IsNullOrWhiteSpace(req.Code))
    {
        logger.LogInformation("Received code payload (first 512 chars): {CodePreview}",
            req.Code?.Length > 512 ? req.Code[..512] : req.Code);
    }

    try
    {
        // Convert JsonElement → real .NET types
        var typedVars = req.Variables?.ToDictionary(
            kvp => kvp.Key,
            kvp => Utils.ConvertJsonElement(kvp.Value)
        ) ?? new();

        Type? functions = null;

        // if code has been sent, compile it and add it to the expression runner
        if (req.Code is not null)
        {
            var assembly = await CompilationCache.CompileCodeAsync(req.Code, logger);
            if (assembly is null)
            {
                logger.LogWarning("Code compilation failed or returned no assembly");
                return Results.BadRequest("Code compilation failed or returned no assembly");
            }

            // optional: load types or register functions from assembly
            logger.LogInformation("Compiled assembly loaded: {Name}", assembly.FullName);

            // get type of the compiled class

            //// Create an instance of the compiled class
            functions = assembly.GetType("Functions");
        }

        var lines = req.Expression.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var result = string.Empty;
        int lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;

            // run each expression using PaleyExpressions.Runner
            var lineResult = new ExpressionRunner(
                line,
                functions).Interpret(typedVars);

            result = result + line + Environment.NewLine + " > " + (lineResult ?? "Null") + Environment.NewLine;

            logger.LogInformation($"Returning expression {lineNumber} result: {JsonSerializer.Serialize(lineResult)}");
        }

        return Results.Content(result, "text/plain");
        //return Results.Ok(result ?? "Null");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Expression evaluation failed");
        return Results.BadRequest(ex.Message);
    }
})
.WithName("EvaluateExpression");


/// <summary>
/// Compiles C# code dynamically using Roslyn and returns the generated assembly.
/// </summary>
app.Run();

public record BuiltinDetails(string Name, string ReturnType, string Signature) { };
