using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EffiTex.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EffiTexDbContext>
{
    public EffiTexDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EffiTexDbContext>()
            .UseNpgsql("Host=localhost;Database=effitex_design;Username=postgres;Password=postgres")
            .Options;
        return new EffiTexDbContext(options);
    }
}
