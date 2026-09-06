namespace PaleyExpressionsUI.Web;

public class ExpressionApiClient(HttpClient httpClient)
{
    public async Task<string> GetExpressionAsync(string expression, Dictionary<string, object?> variables, string? code = null)
    {
        var payload = new
        {
            expression,
            variables,
            code
        };

        Console.WriteLine("CLIENT PAYLOAD: " +
            System.Text.Json.JsonSerializer.Serialize(payload));

        var response = await httpClient.PostAsJsonAsync("/expression", payload);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Expression evaluation failed ({response.StatusCode}). Server returned: {body}"
            );
        }

        return body;
    }

    
    public async Task<BuiltinDetails[]> GetBuiltinsAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<BuiltinDetails>? builtins = null;

        await foreach (var builtin in httpClient.GetFromJsonAsAsyncEnumerable<BuiltinDetails>("/builtins", cancellationToken))
        {
            if (builtin is not null)
            {
                builtins ??= [];
                builtins.Add(builtin);
            }
        }

        return builtins?.ToArray() ?? [];
    }
}

public record BuiltinDetails(string Name, string ReturnType, string Signature) { };
