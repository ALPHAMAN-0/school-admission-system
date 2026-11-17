using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolAdmission.Api.Data;

/// <summary>Used only by `dotnet ef migrations add`; never connects to a database.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdmissionDbContext>
{
    public AdmissionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only")
            .Options;

        return new AdmissionDbContext(options);
    }
}
