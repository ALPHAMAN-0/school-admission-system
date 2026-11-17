namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>One fresh database and one in-memory API per test class.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public TestDatabase Database { get; private set; } = null!;

    public ManualTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public AdmissionApiFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = await TestDatabase.CreateAsync();
        Factory = new AdmissionApiFactory(Database.ConnectionString, Time);
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
