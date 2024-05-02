using FluentAssertions;
using Npgsql;
using Tools.Tools;
using Xunit;

namespace EventStoreBasics.Tests;

public class Exercise05StreamAggregation
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; } = default!;
        public long Version { get; private set; }

        public User(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        // For deserialization
        private User() { }

        public void Apply(UserCreated @event)
        {
            Id = @event.UserId;
            Name = @event.UserName;
        }

        public void Apply(UserNameUpdated @event)
        {
            Name = @event.UserName;
        }
    }

    public class UserCreated(Guid userId, string userName)
    {
        public Guid UserId { get; } = userId;
        public string UserName { get; } = userName;
    }

    public class UserNameUpdated(Guid userId, string userName)
    {
        public Guid UserId { get; } = userId;
        public string UserName { get; } = userName;
    }

    private readonly NpgsqlConnection databaseConnection;
    private readonly EventStore eventStore;

    /// <summary>
    /// Inits Event Store
    /// </summary>
    public Exercise05StreamAggregation()
    {
        databaseConnection = PostgresDbConnectionProvider.GetFreshDbConnection();

        // Create Event Store
        eventStore = new EventStore(databaseConnection);

        // Initialize Event Store
        eventStore.Init();
    }

    [Fact]
    [Trait("Category", "SkipCI")]
    public void AggregateStream_ShouldReturnObjectWithStateBasedOnEvents()
    {
        var streamId = Guid.NewGuid();
        var userCreated = new UserCreated(streamId, "John Doe");
        var userNameUpdated = new UserNameUpdated(streamId, "Adam Smith");

        eventStore.AppendEvent<User>(streamId, userCreated);
        eventStore.AppendEvent<User>(streamId, userNameUpdated);

        var aggregate = eventStore.AggregateStream<User>(streamId);

        aggregate.Id.Should().Be(streamId);
        aggregate.Name.Should().Be(userNameUpdated.UserName);
        aggregate.Version.Should().Be(2);
    }
}
