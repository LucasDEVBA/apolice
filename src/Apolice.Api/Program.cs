var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi("v1");

var app = builder.Build();
app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok());
app.Run();

public partial class Program;
