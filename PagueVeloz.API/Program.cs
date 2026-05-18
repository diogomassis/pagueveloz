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
    dbContext.Database.EnsureCreated();
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

static async Task<IResult> HandleProcessTransactionAsync(ProcessTransactionRequest request, ITransactionProcessor processor, CancellationToken cancellationToken)
{
    var result = await processor.ProcessAsync(request, cancellationToken);
    return result.ErrorMessage is null ? Results.Ok(result) : Results.BadRequest(result);
}
