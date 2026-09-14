using Meadow.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Data.Sql;

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
    public DbSet<TreatmentReason> TreatmentReasons => Set<TreatmentReason>();
    public DbSet<ClawFinding> ClawFindings => Set<ClawFinding>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // Der Unique-Index auf Cow.Ear_Tag_Number. Er stand als [Index]-Attribut am Modell,
    // aber Meadow.Shared soll paketfrei bleiben (der WebAssembly-Client laedt es mit),
    // und das Attribut kommt aus Microsoft.EntityFrameworkCore.
    //
    // Name und Eindeutigkeit sind absichtlich woertlich uebernommen: das Modell ist damit
    // identisch zum Snapshot, es entsteht KEINE neue Migration. Nachpruefbar mit
    // "dotnet ef migrations has-pending-model-changes".
    //
    // Das ist die einzige Fluent-Konfiguration im Kontext; alles andere sind Annotationen
    // an den Modellen. Wer hier etwas ergaenzt, sollte wissen, dass er damit anfaengt, die
    // Konfiguration auf zwei Orte zu verteilen.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cow>()
            .HasIndex(c => c.EarTagNumber)
            .IsUnique()
            .HasDatabaseName("IX_Cow_Ear_Tag_Number");
    }
}
