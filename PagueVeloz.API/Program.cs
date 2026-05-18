using System.Diagnostics;
using Npgsql;
using PagueVeloz.Application;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

var webBuilder = WebApplication.CreateBuilder(args);

webBuilder.Services.AddOpenApi();
webBuilder.Services.AddSwaggerGen();
webBuilder.Services.AddApplication();
webBuilder.Services.AddInfrastructure(webBuilder.Configuration);

var app = webBuilder.Build();

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
            try
            {
                dbContext.Database.EnsureCreated();
            }
            finally
            {
                using var rel = new NpgsqlCommand($"SELECT pg_advisory_unlock({advisoryLockKey});", conn);
                rel.ExecuteScalar();
            }
        }
        else
        {
            // Could not acquire lock in time — skip migration to avoid contention.
            // The instance that acquired the lock will perform initialization.
        }
    }
    catch (Exception)
    {
        try
        {
            dbContext.Database.EnsureCreated();
        }
        catch { }
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

static async Task<IResult> HandleCreateAccountAsync(CreateAccountRequest request, IAccountService accountService, CancellationToken cancellationToken)
{
    var result = await accountService.CreateAsync(request, cancellationToken);
    return result.ErrorMessage is null ? Results.Ok(result) : Results.BadRequest(result);
}

static async Task<IResult> HandleProcessTransactionAsync(ProcessTransactionRequest request, ITransactionProcessor processor, HttpRequest httpRequest, CancellationToken cancellationToken)
{
    var req = request;
    if (string.IsNullOrWhiteSpace(req.ReferenceId))
    {
        if (httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemp) && !string.IsNullOrWhiteSpace(idemp))
        {
            req = req with { ReferenceId = idemp.ToString() };
        }
    }
    var result = await processor.ProcessAsync(req, cancellationToken);
    return result.ErrorMessage is null ? Results.Ok(result) : Results.BadRequest(result);
}
