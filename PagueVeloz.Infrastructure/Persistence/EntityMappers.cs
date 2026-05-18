using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure.Persistence;

public static class EntityMappers
{
    public static AccountDomain ToDomain(this AccountEntity entity)
    {
        var account = new AccountDomain(entity.ClientId, entity.AccountId, entity.Balance, entity.CreditLimit, ParseStatus(entity.Status));
        if (entity.ReservedBalance > 0)
        {
            var reservedField = typeof(AccountDomain).GetProperty(nameof(AccountDomain.ReservedBalance));
            reservedField?.SetValue(account, entity.ReservedBalance);
        }
        return account;
    }

    public static AccountEntity ToEntity(this AccountDomain account)
    {
        return new AccountEntity
        {
            AccountId = account.AccountId,
            ClientId = account.ClientId,
            Balance = account.Balance,
            ReservedBalance = account.ReservedBalance,
            CreditLimit = account.CreditLimit,
            Status = account.Status.ToString()
        };
    }

    public static ProcessTransactionResponseEntity ToEntity(this PagueVeloz.Application.Dtos.ProcessTransactionResponse response, string referenceId)
    {
        return new ProcessTransactionResponseEntity
        {
            ReferenceId = referenceId,
            TransactionId = response.TransactionId,
            Status = response.Status,
            Balance = response.Balance,
            ReservedBalance = response.ReservedBalance,
            AvailableBalance = response.AvailableBalance,
            Timestamp = response.Timestamp,
            ErrorMessage = response.ErrorMessage
        };
    }

    private static EnumAccountStatus ParseStatus(string status)
    {
        if (!Enum.TryParse<EnumAccountStatus>(status, true, out var parsedStatus))
        {
            return EnumAccountStatus.Active;
        }
        return parsedStatus;
    }
}
