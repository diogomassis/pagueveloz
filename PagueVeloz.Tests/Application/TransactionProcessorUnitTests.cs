using System.Text.Json;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Events;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

namespace PagueVeloz.Tests.Application;

public sealed class TransactionProcessorUnitTests
{
    [Fact]
    public async Task ProcessAsync_returns_failed_when_source_not_found()
    {
        var fixtures = new TestFixtures();
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "credit",
            "NOACC",
            100,
            "BRL",
            "R-1"));
        Assert.Equal("failed", res.Status);
        Assert.Contains("Source account not found", res.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_transfer_requires_target_account_id()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1"));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "transfer",
            "A1",
            10,
            "BRL",
            "R-2"));
        Assert.Equal("failed", res.Status);
        Assert.Contains("Target account id is required", res.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_transfer_target_not_found()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 100));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "transfer",
            "A1",
            50,
            "BRL",
            "R-3",
            "MISSING"));
        Assert.Equal("failed", res.Status);
        Assert.Contains("Target account not found", res.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_debit_insufficient_balance()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 0, 0));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "debit",
            "A1",
            10,
            "BRL",
            "R-4"));
        Assert.Equal("failed", res.Status);
        Assert.Contains("Insufficient", res.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_reserve_insufficient()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 10));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "reserve",
            "A1",
            20,
            "BRL",
            "R-5"));
        Assert.Equal("failed", res.Status);
    }

    [Fact]
    public async Task ProcessAsync_capture_insufficient_reserved()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 100));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "capture",
            "A1",
            50,
            "BRL",
            "R-6"));
        Assert.Equal("failed", res.Status);
    }

    [Fact]
    public async Task ProcessAsync_reversal_requires_metadata()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 100));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "reversal",
            "A1",
            10,
            "BRL",
            "R-7"));
        Assert.Equal("failed", res.Status);
        Assert.Contains("Reversal requires metadata", res.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_reversal_for_credit_restores_balance()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 100));
        var credit = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "credit",
            "A1",
            50,
            "BRL",
            "R-8"));
        Assert.Equal("success", credit.Status);
        var metadata = JsonDocument.Parse("{\"original_operation\":\"credit\"}").RootElement;
        var rev = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "reversal",
            "A1",
            50,
            "BRL",
            "R-9",
            null,
            metadata));
        Assert.Equal("success", rev.Status);
        var acc = await fixtures.AccountRepository.GetByIdAsync("A1");
        Assert.Equal(100, acc!.Balance);
    }

    [Fact]
    public async Task ProcessAsync_returns_cached_response_when_idempotent()
    {
        var fixtures = new TestFixtures();
        await fixtures.AccountService.CreateAsync(new CreateAccountRequest("C", "A1", 100));
        var cached = new ProcessTransactionResponse("ID-PROCESSED", "success", 100, 0, 100, DateTimeOffset.UtcNow, null);
        await fixtures.IdempotencyStore.SaveAsync("ID-1", cached);
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "credit",
            "A1",
            10,
            "BRL",
            "ID-1"));
        Assert.Equal(cached.TransactionId, res.TransactionId);
        var acc = await fixtures.AccountRepository.GetByIdAsync("A1");
        Assert.Equal(100, acc!.Balance); // unchanged
    }

    [Fact]
    public async Task ProcessAsync_handles_save_exceptions_and_returns_failed()
    {
        var fixtures = new TestFixtures(throwOnSave: true);
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 100));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "credit",
            "A1",
            10,
            "BRL",
            "R-10"));
        Assert.Equal("failed", res.Status);
    }

    [Fact]
    public async Task ProcessAsync_publishes_event_on_success()
    {
        var fixtures = new TestFixtures();
        await fixtures.SeedAccountAsync(new PagueVeloz.Domain.AccountDomain("C", "A1", 0));
        var res = await fixtures.TransactionProcessor.ProcessAsync(new ProcessTransactionRequest(
            "credit",
            "A1",
            100,
            "BRL",
            "R-11"));
        Assert.Equal("success", res.Status);
        Assert.Single(fixtures.PublishedEvents);
        Assert.Equal(res.TransactionId, fixtures.PublishedEvents[0].TransactionId);
    }

    private sealed class TestFixtures
    {
        private InMemoryAccountRepository? _innerRepo;

        public TestFixtures(bool throwOnSave = false)
        {
            if (throwOnSave)
            {
                _innerRepo = new InMemoryAccountRepository();
                AccountRepository = new ThrowingSaveRepository(_innerRepo);
            }
            else
            {
                AccountRepository = new InMemoryAccountRepository();
            }
            IdempotencyStore = new InMemoryIdempotencyStore();
            EventPublisher = new RecordingEventPublisher(PublishedEvents);
            LockProvider = new InMemoryAccountLockProvider();
            UnitOfWork = new InMemoryUnitOfWork();
            AccountService = new AccountService(AccountRepository, UnitOfWork);
            TransactionProcessor = new TransactionProcessor(AccountRepository, IdempotencyStore, LockProvider, EventPublisher, UnitOfWork);
        }

        public PagueVeloz.Application.Abstractions.IAccountRepository AccountRepository { get; }
        public InMemoryIdempotencyStore IdempotencyStore { get; }
        public PagueVeloz.Application.Abstractions.IEventPublisher EventPublisher { get; }
        public InMemoryAccountLockProvider LockProvider { get; }
        public InMemoryUnitOfWork UnitOfWork { get; }
        public AccountService AccountService { get; }
        public TransactionProcessor TransactionProcessor { get; }
        public List<TransactionProcessedEvent> PublishedEvents { get; } = new();

        public Task SeedAccountAsync(PagueVeloz.Domain.AccountDomain account)
        {
            if (_innerRepo is not null)
                return _innerRepo.SaveAsync(account);
            return AccountRepository.SaveAsync(account);
        }

        private sealed class ThrowingSaveRepository : PagueVeloz.Application.Abstractions.IAccountRepository
        {
            private readonly InMemoryAccountRepository _inner;
            public ThrowingSaveRepository(InMemoryAccountRepository inner) => _inner = inner;

            public Task<PagueVeloz.Domain.AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
                => _inner.GetByIdAsync(accountId, cancellationToken);

            public Task SaveAsync(PagueVeloz.Domain.AccountDomain account, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("save failure");

            public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
                => _inner.ExistsAsync(accountId, cancellationToken);
        }

        private sealed class RecordingEventPublisher : PagueVeloz.Application.Abstractions.IEventPublisher
        {
            private readonly List<TransactionProcessedEvent> _target;
            public RecordingEventPublisher(List<TransactionProcessedEvent> target) => _target = target;
            public Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
            {
                _target.Add(@event);
                return Task.CompletedTask;
            }
        }
    }
}
