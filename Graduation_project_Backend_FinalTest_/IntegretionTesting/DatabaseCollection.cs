using Xunit;

[CollectionDefinition("db")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
