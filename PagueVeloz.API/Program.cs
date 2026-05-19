using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using PagueVeloz.Application;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

var webBuilder = WebApplication.CreateBuilder(args);

// Configure structured JSON logging to console for better observability
webBuilder.Logging.ClearProviders();
webBuilder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "o"; // ISO 8601
});
webBuilder.Logging.SetMinimumLevel(LogLevel.Information);

webBuilder.Services.AddOpenApi();
webBuilder.Services.AddSwaggerGen();
webBuilder.Services.AddApplication();
webBuilder.Services.AddInfrastructure(webBuilder.Configuration);

var app = webBuilder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

if (!string.IsNullOrWhiteSpace(webBuilder.Configuration.GetConnectionString("PagueVeloz")))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PagueVelozDbContext>();
    var connString = webBuilder.Configuration.GetConnectionString("PagueVeloz")!;
    const long advisoryLockKey = 156789123456789;
    try
    {
        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        logger.LogInformation("Attempting to acquire advisory lock {LockKey} for DB initialization", advisoryLockKey);
        var sw = Stopwatch.StartNew();
        var acquired = false;
        while (!acquired && sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            using var cmd = new NpgsqlCommand($"SELECT pg_try_advisory_lock({advisoryLockKey});", conn);
            var res = cmd.ExecuteScalar();
            if (res is bool b && b) { acquired = true; break; }
            Thread.Sleep(500);
        }
        if (acquired)
        {
            logger.LogInformation("Acquired advisory lock {LockKey}; running EnsureCreated()", advisoryLockKey);
            try
            {
                dbContext.Database.EnsureCreated();
                logger.LogInformation("Database initialization EnsureCreated() completed");
            }
            finally
            {
                using var rel = new NpgsqlCommand($"SELECT pg_advisory_unlock({advisoryLockKey});", conn);
                rel.ExecuteScalar();
                logger.LogInformation("Released advisory lock {LockKey}", advisoryLockKey);
            }
        }
        else
        {
            logger.LogWarning("Could not acquire advisory lock {LockKey} within timeout; skipping EnsureCreated() to avoid contention", advisoryLockKey);
        }
    }
    catch (Exception)
    {
        logger.LogWarning("Failed to acquire advisory lock or connect to DB; attempting best-effort EnsureCreated() without lock");
        try
        {
            dbContext.Database.EnsureCreated();
            logger.LogInformation("Best-effort EnsureCreated() completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Best-effort EnsureCreated() failed");
        }
    }
}

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "PagueVeloz API");
});

app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapPost("/accounts", HandleCreateAccountAsync);
api.MapPost("/transactions", HandleProcessTransactionAsync);

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

static async Task<IResult> HandleCreateAccountAsync(CreateAccountRequest request, IAccountService accountService, ILogger<Program> logger, CancellationToken cancellationToken)
{
    logger.LogInformation("CreateAccount request received {ClientId} {AccountId}", request.ClientId, request.AccountId);
    var result = await accountService.CreateAsync(request, cancellationToken);
    if (result.ErrorMessage is null)
    {
        logger.LogInformation("CreateAccount succeeded {AccountId}", result.AccountId);
        return Results.Ok(result);
    }
    logger.LogWarning("CreateAccount failed {AccountId} {Error}", result.AccountId, result.ErrorMessage);
    return Results.BadRequest(result);
}

static async Task<IResult> HandleProcessTransactionAsync(ProcessTransactionRequest request, ITransactionProcessor processor, HttpRequest httpRequest, ILogger<Program> logger, CancellationToken cancellationToken)
{
    var req = request;
    if (string.IsNullOrWhiteSpace(req.ReferenceId))
    {
        if (httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemp) && !string.IsNullOrWhiteSpace(idemp))
        {
            req = req with { ReferenceId = idemp.ToString() };
        }
    }
    logger.LogInformation("ProcessTransaction request {Operation} {AccountId} {ReferenceId}", req.Operation, req.AccountId, req.ReferenceId);
    var result = await processor.ProcessAsync(req, cancellationToken);
    if (result.ErrorMessage is null)
    {
        logger.LogInformation("Transaction processed {TransactionId} {Status}", result.TransactionId, result.Status);
        return Results.Ok(result);
    }
    logger.LogWarning("Transaction failed {ReferenceId} {Error}", req.ReferenceId, result.ErrorMessage);
    return Results.BadRequest(result);
}
