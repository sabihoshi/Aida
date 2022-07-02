using HuTao.Data.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HuTao.Data;

public class HuTaoContextFactory : IDesignTimeDbContextFactory<HuTaoContext>
{
    public HuTaoContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HuTaoContext>()
            .UseNpgsql(HuTaoConfig.Configuration.AidaContext);

        return new HuTaoContext(optionsBuilder.Options);
    }
}