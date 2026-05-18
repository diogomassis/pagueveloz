namespace PagueVeloz.Application.Dtos;

public sealed record CreateAccountRequest(
    string ClientId,
    string AccountId,
    long InitialBalance = 0,
    long CreditLimit = 0);