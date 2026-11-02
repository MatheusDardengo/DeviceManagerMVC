namespace DeviceManagerMVC.IntegrationTests.Infrastructure.Database;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
