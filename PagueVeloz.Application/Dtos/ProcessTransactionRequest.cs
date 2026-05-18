using System.Text.Json;

namespace PagueVeloz.Application.Dtos;

public sealed record ProcessTransactionRequest(
    string Operation,
    string AccountId,
    long Amount,
    string Currency,
    string ReferenceId,
    string? TargetAccountId = null,
    JsonElement? Metadata = null);