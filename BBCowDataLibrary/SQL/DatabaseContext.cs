using BB_Cow.Class;
using Microsoft.EntityFrameworkCore;

namespace BBCowDataLibrary.SQL;

public sealed class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
    }

    public DbSet<Cow> Cows => Set<Cow>();
    public DbSet<KPI> KPIs => Set<KPI>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<PlannedCowTreatment> PlannedCowTreatments => Set<PlannedCowTreatment>();
    public DbSet<PlannedClawTreatment> PlannedClawTreatments => Set<PlannedClawTreatment>();
    public DbSet<ClawTreatment> ClawTreatments => Set<ClawTreatment>();
    public DbSet<CowTreatment> CowTreatments => Set<CowTreatment>();
    public DbSet<Udder> Udders => Set<Udder>();
    public DbSet<WhereHow> WhereHows => Set<WhereHow>();
}
