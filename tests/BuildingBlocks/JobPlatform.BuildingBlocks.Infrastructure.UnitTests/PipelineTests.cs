using FluentValidation;
using JobPlatform.BuildingBlocks.Infrastructure.Behaviors;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

public sealed record EchoCommand(string Text, string? IdempotencyKey = null) : ICommand<string>, IIdempotentCommand;

public sealed record EchoQuery(string Text) : IQuery<string>;

public sealed record RestrictedCommand : ICommand<Unit>, IAuthorizedRequest
{
    public string? RequiredPermission => "x.y";
}

public sealed record LimitedCommand : ICommand<Unit>, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "limited";
    public string RateLimitedErrorCode => "E-LIMITED";
    public string UniqueViolationErrorCode => "E-DUP-CUSTOM";
}

public sealed record PersistingCommand : ICommand<Unit>, IPersistOnFailure;

public sealed class EchoValidator : AbstractValidator<EchoCommand>
{
    public EchoValidator() => RuleFor(x => x.Text).NotEmpty().WithErrorCode("VAL.Text.Required");
}

public sealed class EchoHandler : ICommandHandler<EchoCommand, string>, IQueryHandler<EchoQuery, string>
{
    public int Calls { get; private set; }

    public Task<Result<string>> Handle(EchoCommand request, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<Result<string>>("echo:" + request.Text);
    }

    public Task<Result<string>> Handle(EchoQuery request, CancellationToken ct) => Task.FromResult<Result<string>>("query:" + request.Text);
}

public class SenderAndBehaviorTests
{
    private static ServiceProvider Build(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddBuildingBlocks(configuration);
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddSingleton<EchoHandler>();
        services.AddScoped<IRequestHandler<EchoCommand, string>>(sp => sp.GetRequiredService<EchoHandler>());
        services.AddScoped<IRequestHandler<EchoQuery, string>>(sp => sp.GetRequiredService<EchoHandler>());
        services.AddScoped<IValidator<EchoCommand>, EchoValidator>();
        services.AddScoped(_ => AppUser());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddScoped(_ => Substitute.For<IAccessAuthorizer>());
        extra?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static ICurrentUser AppUser(string source = "src")
    {
        var user = Substitute.For<ICurrentUser>();
        user.SourceKey.Returns(source);
        user.UserId.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        return user;
    }

    [Fact]
    public async Task Sender_RunsTheHandlerThroughTheWholePipeline()
    {
        using var provider = Build();
        var sender = provider.CreateScope().ServiceProvider.GetRequiredService<ISender>();

        var command = await sender.Send(new EchoCommand("hi"));
        var query = await sender.Send(new EchoQuery("hi"));

        command.Value.Should().Be("echo:hi");
        query.Value.Should().Be("query:hi");
    }

    [Fact]
    public async Task ValidationBehavior_ReturnsFieldErrorsAndNeverCallsTheHandler()
    {
        using var provider = Build();
        var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new EchoCommand(""));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.ValidationErrors!["text"].Should().Equal("VAL.Text.Required");
        provider.GetRequiredService<EchoHandler>().Calls.Should().Be(0);
    }

    [Fact]
    public async Task ValidationBehavior_CamelCasesNestedPropertyPaths()
    {
        var behavior = new ValidationBehavior<NestedCommand, string>(new IValidator<NestedCommand>[] { new NestedValidator() });

        var result = await behavior.Handle(new NestedCommand(new Inner("")), () => Task.FromResult<Result<string>>("ok"), default);

        result.Error!.ValidationErrors!.Keys.Should().Contain("inner.Name".Replace("Name", "name"));
    }

    public sealed record Inner(string Name);

    public sealed record NestedCommand(Inner Inner) : ICommand<string>;

    public sealed class NestedValidator : AbstractValidator<NestedCommand>
    {
        public NestedValidator() => RuleFor(x => x.Inner.Name).NotEmpty().WithErrorCode("VAL.Name.Required");
    }

    [Fact]
    public async Task AuthorizationBehavior_SkipsAnonymousRequests_AndBlocksWhenTheAuthorizerRefuses()
    {
        var authorizer = Substitute.For<IAccessAuthorizer>();
        authorizer.AuthorizeAsync(default!, default!, default!, default).ReturnsForAnyArgs(Error.Forbidden("E-NO", "no"));
        var behavior = new AuthorizationBehavior<RestrictedCommand, Unit>(authorizer, AppUser());
        var anonymous = new AuthorizationBehavior<EchoQuery, string>(authorizer, AppUser());

        var blocked = await behavior.Handle(new RestrictedCommand(), () => Task.FromResult(Result.Success()), default);
        var passed = await anonymous.Handle(new EchoQuery("x"), () => Task.FromResult<Result<string>>("ok"), default);

        blocked.Error!.Code.Should().Be("E-NO");
        passed.Value.Should().Be("ok");
        await authorizer.Received(1).AuthorizeAsync(Arg.Any<ICurrentUser>(), Arg.Any<IAuthorizedRequest>(), nameof(RestrictedCommand), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizationBehavior_AllowsWhenTheAuthorizerAllows()
    {
        var authorizer = Substitute.For<IAccessAuthorizer>();
        authorizer.AuthorizeAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());
        var behavior = new AuthorizationBehavior<RestrictedCommand, Unit>(authorizer, AppUser());

        (await behavior.Handle(new RestrictedCommand(), () => Task.FromResult(Result.Success()), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RateLimitBehavior_BlocksAfterThePermitsAreSpent_WithRetryAfter()
    {
        var limiter = Substitute.For<IRateLimiter>();
        limiter.HitAsync("limited:src", 5, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>()).Returns(new RateLimitDecision(false, 6, TimeSpan.FromMinutes(3)));
        var behavior = new RateLimitBehavior<LimitedCommand, Unit>(limiter, AppUser());

        var result = await behavior.Handle(new LimitedCommand(), () => Task.FromResult(Result.Success()), default);

        result.Error!.Code.Should().Be("E-LIMITED");
        result.Error.Type.Should().Be(ErrorType.TooManyRequests);
        result.Error.RetryAfter.Should().Be(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task RateLimitBehavior_LetsAllowedRequestsAndUnthrottledRequestsThrough()
    {
        var limiter = Substitute.For<IRateLimiter>();
        limiter.HitAsync(default!, default, default, default).ReturnsForAnyArgs(new RateLimitDecision(true, 1, TimeSpan.Zero));

        (await new RateLimitBehavior<LimitedCommand, Unit>(limiter, AppUser()).Handle(new LimitedCommand(), () => Task.FromResult(Result.Success()), default)).IsSuccess.Should().BeTrue();
        (await new RateLimitBehavior<EchoQuery, string>(limiter, AppUser()).Handle(new EchoQuery("x"), () => Task.FromResult<Result<string>>("ok"), default)).IsSuccess.Should().BeTrue();
        await limiter.ReceivedWithAnyArgs(1).HitAsync(default!, default, default, default);
    }

    [Fact]
    public async Task IdempotencyBehavior_ReplaysTheStoredResponse_AndRunsTheHandlerOnce()
    {
        var store = new CacheIdempotencyStore(new InMemoryCacheStore(new FakeTimeProvider(), Options.Create(new CacheOptions())));
        var behavior = new IdempotencyBehavior<EchoCommand, string>(store, AppUser());
        var calls = 0;
        Task<Result<string>> Next() { calls++; return Task.FromResult<Result<string>>("first"); }

        var first = await behavior.Handle(new EchoCommand("a", "key-1"), Next, default);
        var second = await behavior.Handle(new EchoCommand("a", "key-1"), Next, default);

        first.Value.Should().Be("first");
        second.Value.Should().Be("first");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task IdempotencyBehavior_RejectsAKeyReusedWithADifferentPayload_AndReportsInFlightRequests()
    {
        var store = new CacheIdempotencyStore(new InMemoryCacheStore(new FakeTimeProvider(), Options.Create(new CacheOptions())));
        var behavior = new IdempotencyBehavior<EchoCommand, string>(store, AppUser());
        await behavior.Handle(new EchoCommand("a", "key-1"), () => Task.FromResult<Result<string>>("first"), default);

        var reused = await behavior.Handle(new EchoCommand("different", "key-1"), () => Task.FromResult<Result<string>>("x"), default);
        reused.Error!.Code.Should().Be("E-IDEMPOTENCY-KEY-REUSED");
        reused.Error.Type.Should().Be(ErrorType.BusinessRule);

        var release = new TaskCompletionSource();
        var slow = behavior.Handle(new EchoCommand("a", "in-flight"), async () =>
        {
            await release.Task;
            return Result.Success("done");
        }, default);

        var inFlight = await behavior.Handle(new EchoCommand("a", "in-flight"), () => Task.FromResult<Result<string>>("x"), default);
        release.SetResult();

        inFlight.Error!.Type.Should().Be(ErrorType.Conflict);
        inFlight.Error.Code.Should().Be("E-IDEMPOTENCY-IN-PROGRESS");
        (await slow).Value.Should().Be("done");
    }

    [Fact]
    public async Task IdempotencyBehavior_DoesNotCacheFailures_AndReleasesTheKeyOnExceptions()
    {
        var store = new CacheIdempotencyStore(new InMemoryCacheStore(new FakeTimeProvider(), Options.Create(new CacheOptions())));
        var behavior = new IdempotencyBehavior<EchoCommand, string>(store, AppUser());

        var failed = await behavior.Handle(new EchoCommand("a", "k"), () => Task.FromResult<Result<string>>(Error.Conflict("E", "m")), default);
        var thrown = () => behavior.Handle(new EchoCommand("a", "k"), () => throw new InvalidOperationException("boom"), default);
        await thrown.Should().ThrowAsync<InvalidOperationException>();
        var retried = await behavior.Handle(new EchoCommand("a", "k"), () => Task.FromResult<Result<string>>("fine"), default);

        failed.IsFailure.Should().BeTrue();
        retried.Value.Should().Be("fine");
    }

    [Fact]
    public async Task IdempotencyBehavior_WithoutAKey_JustRuns()
    {
        var store = Substitute.For<IIdempotencyStore>();
        var behavior = new IdempotencyBehavior<EchoCommand, string>(store, AppUser());

        (await behavior.Handle(new EchoCommand("a"), () => Task.FromResult<Result<string>>("x"), default)).Value.Should().Be("x");
        await store.DidNotReceiveWithAnyArgs().TryBeginAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task LoggingBehavior_PassesResultsThroughAndRethrowsExceptions()
    {
        var correlation = Substitute.For<ICorrelationContext>();
        var behavior = new LoggingBehavior<EchoCommand, string>(NullLogger<LoggingBehavior<EchoCommand, string>>.Instance, correlation);

        (await behavior.Handle(new EchoCommand("a"), () => Task.FromResult<Result<string>>("ok"), default)).Value.Should().Be("ok");
        (await behavior.Handle(new EchoCommand("a"), () => Task.FromResult<Result<string>>(Error.Conflict("E", "m")), default)).IsFailure.Should().BeTrue();
        var thrown = () => behavior.Handle(new EchoCommand("a"), () => throw new InvalidOperationException("boom"), default);
        await thrown.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class UnitOfWorkBehaviorTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly DomainEventBuffer _events = new();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();

    private UnitOfWorkBehavior<TRequest, Unit> Behavior<TRequest>() where TRequest : IRequest<Unit> => new(_uow, _events, _dispatcher);

    private sealed record Ping : ICommand<Unit>;

    private sealed record PingEvent(DateTime At) : DomainEvent(At);

    [Fact]
    public async Task Command_Success_BeginsSavesCommitsAndThenDispatchesDomainEvents()
    {
        _events.Add(new PingEvent(DateTime.UtcNow));

        var result = await Behavior<Ping>().Handle(new Ping(), () => Task.FromResult(Result.Success()), default);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _uow.SaveChangesAsync(Arg.Any<CancellationToken>());
            _uow.CommitTransactionAsync(Arg.Any<CancellationToken>());
            _dispatcher.DispatchAsync(Arg.Any<IReadOnlyCollection<IDomainEvent>>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Command_Failure_RollsBackWithoutSaving()
    {
        var result = await Behavior<Ping>().Handle(new Ping(), () => Task.FromResult<Result<Unit>>(Error.Conflict("E", "m")), default);

        result.IsFailure.Should().BeTrue();
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await _uow.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
    }

    [Fact]
    public async Task PersistOnFailureCommand_CommitsEvenThoughTheResultIsAFailure()
    {
        var result = await Behavior<PersistingCommand>().Handle(new PersistingCommand(), () => Task.FromResult<Result<Unit>>(Error.Unauthorized("E", "m")), default);

        result.IsFailure.Should().BeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Queries_PassThroughWithoutATransaction()
    {
        var behavior = new UnitOfWorkBehavior<EchoQuery, string>(_uow, _events, _dispatcher);

        var result = await behavior.Handle(new EchoQuery("x"), () => Task.FromResult<Result<string>>("ok"), default);

        result.Value.Should().Be("ok");
        await _uow.DidNotReceiveWithAnyArgs().BeginTransactionAsync(default);
    }

    [Fact]
    public async Task BusinessRuleViolation_RollsBackAndBecomesATypedResult()
    {
        var result = await Behavior<Ping>().Handle(new Ping(),
            () => throw new BusinessRuleViolationException("AI.X.Y", "nope", "E-X", BusinessRuleKind.Conflict), default);

        result.Error!.Code.Should().Be("E-X");
        result.Error.RuleCode.Should().Be("AI.X.Y");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        await _uow.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrencyAndUniqueViolations_BecomeConflicts_WithTheCommandsOwnDuplicateCode()
    {
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<Task<int>>(_ => throw new ConcurrencyConflictException(new Exception()));
        var concurrency = await Behavior<Ping>().Handle(new Ping(), () => Task.FromResult(Result.Success()), default);
        concurrency.Error!.Code.Should().Be("E-CONCURRENCY-CONFLICT");

        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<Task<int>>(_ => throw new UniqueConstraintViolationException(new Exception()));
        var custom = await Behavior<LimitedCommand>().Handle(new LimitedCommand(), () => Task.FromResult(Result.Success()), default);
        var generic = await Behavior<Ping>().Handle(new Ping(), () => Task.FromResult(Result.Success()), default);

        custom.Error!.Code.Should().Be("E-DUP-CUSTOM");
        generic.Error!.Code.Should().Be("E-DUPLICATE");
        custom.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UnexpectedExceptions_RollBackAndPropagate()
    {
        var act = () => Behavior<Ping>().Handle(new Ping(), () => throw new InvalidOperationException("boom"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _uow.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }
}

public class ResultAndErrorTests
{
    [Fact]
    public void Result_SuccessAndFailure_ExposeValueOrError()
    {
        Result<int> ok = 5;
        Result<int> failed = Error.NotFound("E", "m");

        ok.IsSuccess.Should().BeTrue();
        ok.Value.Should().Be(5);
        ok.Match(v => v * 2, _ => -1).Should().Be(10);
        failed.IsFailure.Should().BeTrue();
        failed.Match(_ => 0, e => e.Code.Length).Should().Be(1);
        var read = () => failed.Value;
        read.Should().Throw<InvalidOperationException>();
        Result.Success().IsSuccess.Should().BeTrue();
        Result.Failure<string>(Error.Unexpected("E", "m")).Error!.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void Error_Factories_SetTheTypes()
    {
        Error.Validation(new Dictionary<string, string[]> { ["a"] = new[] { "x" } }).Type.Should().Be(ErrorType.Validation);
        Error.BusinessRule("E", "m").Type.Should().Be(ErrorType.BusinessRule);
        Error.Forbidden("E", "m").Type.Should().Be(ErrorType.Forbidden);
        Error.Unauthorized("E", "m").Type.Should().Be(ErrorType.Unauthorized);
        Error.External("E", "m").Type.Should().Be(ErrorType.External);
        Error.TooManyRequests("E", "m", TimeSpan.FromSeconds(3)).RetryAfter.Should().Be(TimeSpan.FromSeconds(3));
        Error.Conflict("E", "m").Type.Should().Be(ErrorType.Conflict);
    }

    [Theory]
    [InlineData(BusinessRuleKind.BusinessRule, ErrorType.BusinessRule)]
    [InlineData(BusinessRuleKind.Conflict, ErrorType.Conflict)]
    [InlineData(BusinessRuleKind.Forbidden, ErrorType.Forbidden)]
    [InlineData(BusinessRuleKind.Unauthorized, ErrorType.Unauthorized)]
    [InlineData(BusinessRuleKind.RateLimited, ErrorType.TooManyRequests)]
    [InlineData(BusinessRuleKind.InvalidInput, ErrorType.Validation)]
    public void BusinessRuleViolation_MapsToTheRightErrorType(BusinessRuleKind kind, ErrorType type)
    {
        var error = new BusinessRuleViolationException("AI.A.B", "m", "E-CODE", kind).ToError();

        error.Type.Should().Be(type);
        error.Code.Should().Be("E-CODE");
        error.RuleCode.Should().Be("AI.A.B");
        new BusinessRuleViolationException("AI.A.B", "m").ToError().Code.Should().Be("AI.A.B", "without an external code the rule code is published");
    }

    [Fact]
    public void BusinessRuleViolation_WithViolations_BecomesFieldErrors()
    {
        var error = new BusinessRuleViolationException("AI.P.W", "weak", "E-AAFR-INVALID-FIELD", BusinessRuleKind.InvalidInput,
            new Dictionary<string, object?> { ["field"] = "password", ["violations"] = new[] { "VAL.Password.MinLength" } }).ToError();

        error.ValidationErrors!["password"].Should().Equal("VAL.Password.MinLength");
    }

    [Fact]
    public void PageRequest_ClampsPagingValues()
    {
        new PageRequestProbe().Should().NotBeNull();
        var request = new JobPlatform.SharedKernel.Application.Paging.PageRequest(0, 1000);
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(100);
        request.Skip.Should().Be(0);
        new JobPlatform.SharedKernel.Application.Paging.PageRequest(3, 10).Skip.Should().Be(20);
        new JobPlatform.SharedKernel.Application.Paging.PagedResult<int>(new[] { 1 }, 1, 10, 25).TotalPages.Should().Be(3);
    }

    private sealed class PageRequestProbe;

    [Fact]
    public void Entity_EqualityIsById_AndValueObjectsCompareByComponents()
    {
        var id = Guid.NewGuid();
        new Widget(id).Should().Be(new Widget(id));
        new Widget(id).Should().NotBe(new Widget(Guid.NewGuid()));
        new Money(5, "USD").Should().Be(new Money(5, "USD"));
        new Money(5, "USD").Should().NotBe(new Money(6, "USD"));
        new Money(5, "USD").GetHashCode().Should().Be(new Money(5, "USD").GetHashCode());
    }

    private sealed class Widget : Entity<Guid>
    {
        public Widget(Guid id) => Id = id;
    }

    private sealed class Money : ValueObject
    {
        private readonly decimal _amount;
        private readonly string _currency;

        public Money(decimal amount, string currency)
        {
            _amount = amount;
            _currency = currency;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return _amount;
            yield return _currency;
        }
    }

    [Fact]
    public void Enums_AreStableContracts()
    {
        Enum.GetNames<ActorType>().Should().Equal("JobSeeker", "Employer", "Administrator", "ExternalJobSite", "Guest", "System");
        Enum.GetNames<Language>().Should().Equal("Ar", "En");
    }
}
