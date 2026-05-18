using PagueVeloz.Application;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("PagueVeloz")))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PagueVelozDbContext>();
    dbContext.Database.EnsureCreated();
}

// Always expose OpenAPI (Swagger) so API documentation is available in all environments
app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "PagueVeloz API");
});

app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapPost("/accounts", async (CreateAccountRequest request, IAccountService accountService, CancellationToken cancellationToken) =>
{
    var response = await accountService.CreateAsync(request, cancellationToken);
    return response.ErrorMessage is null ? Results.Ok(response) : Results.BadRequest(response);
});

api.MapPost("/transactions", async (ProcessTransactionRequest request, ITransactionProcessor transactionProcessor, CancellationToken cancellationToken) =>
{
    var response = await transactionProcessor.ProcessAsync(request, cancellationToken);
    return response.ErrorMessage is null ? Results.Ok(response) : Results.BadRequest(response);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
