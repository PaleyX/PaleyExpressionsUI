var builder = DistributedApplication.CreateBuilder(args);

var expressionService = builder.AddProject<Projects.PaleyExpressionsUI_ExpressionService>("expressionservice");

builder.AddProject<Projects.PaleyExpressionsUI_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(expressionService)
    .WaitFor(expressionService);

builder.Build().Run();
