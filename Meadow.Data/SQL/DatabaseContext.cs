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

    // Die manuell abgebrochenen Behandlungsplan-Durchlaeufe. Durchlaeufe selbst
    // werden nicht gespeichert, sie entstehen beim Rechnen aus den Behandlungen;
    // der Abbruch ist das Einzige, was sich daraus nicht ableiten laesst.
    public DbSet<TreatmentPlanCancellation> TreatmentPlanCancellations => Set<TreatmentPlanCancellation>();

    // Web-Push-Anmeldungen. Kein Client-Cache der dreizehn Dienste, sondern der
    // serverseitige Kanal fuer server-initiierte Benachrichtigungen (Task 3).
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    // Der persistente Entdopplungs-Merker des Verband-Push-Schedulers (Task 6):
    // je Behandlung der Tag der zuletzt versendeten Erinnerung. Rein
    // serverseitig, kein Client laedt ihn - wie PushSubscription.
    public DbSet<PushReminderLog> PushReminderLogs => Set<PushReminderLog>();

    // Der Entdopplungs-Merker der taeglichen Behandlungsplan-Sammel-Push: je
    // Tag hoechstens eine Zeile. Rein serverseitig wie PushReminderLog.
    public DbSet<TreatmentPlanPushLog> TreatmentPlanPushLogs => Set<TreatmentPlanPushLog>();

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

        // Wie die Ohrmarke: eindeutig, aber mehrfach NULL erlaubt (MariaDB-UNIQUE),
        // damit alle noch nicht zugeordneten Kuehe nebeneinander stehen koennen.
        modelBuilder.Entity<Cow>()
            .HasIndex(c => c.LelyAnimalId)
            .IsUnique()
            .HasDatabaseName("IX_Cow_Lely_Animal_Id");

        // Der zweite Schluessel der vier offline schreibbaren Tabellen. Die
        // Eindeutigkeit ist die eigentliche Garantie: sie macht aus einem
        // wiederholten Insert einen No-op, ganz gleich wie oft die Uebertragung
        // abbricht und neu ansetzt. Die Pruefung im Endpunkt ist nur der
        // schnelle Weg dorthin - der Index ist der verlaessliche.
        //
        // ascii statt utf8mb4: eine GUID besteht aus Hexziffern und
        // Bindestrichen. Das macht den Indexschluessel 36 Byte statt 144 und
        // haelt ihn dicht.
        modelBuilder.Entity<CowTreatment>()
            .Property(t => t.ClientId).HasColumnType("char(36)").HasCharSet("ascii");
        modelBuilder.Entity<CowTreatment>()
            .HasIndex(t => t.ClientId).IsUnique().HasDatabaseName("IX_Cow_Treatment_Client_Id");

        modelBuilder.Entity<ClawTreatment>()
            .Property(t => t.ClientId).HasColumnType("char(36)").HasCharSet("ascii");
        modelBuilder.Entity<ClawTreatment>()
            .HasIndex(t => t.ClientId).IsUnique().HasDatabaseName("IX_Claw_Treatment_Client_Id");

        modelBuilder.Entity<PlannedCowTreatment>()
            .Property(t => t.ClientId).HasColumnType("char(36)").HasCharSet("ascii");
        modelBuilder.Entity<PlannedCowTreatment>()
            .HasIndex(t => t.ClientId).IsUnique().HasDatabaseName("IX_Planned_Cow_Treatment_Client_Id");

        modelBuilder.Entity<PlannedClawTreatment>()
            .Property(t => t.ClientId).HasColumnType("char(36)").HasCharSet("ascii");
        modelBuilder.Entity<PlannedClawTreatment>()
            .HasIndex(t => t.ClientId).IsUnique().HasDatabaseName("IX_Planned_Claw_Treatment_Client_Id");

        // Ein Browser = ein Endpoint = eine Zeile. Der Unique-Index ist die
        // eigentliche Garantie der Idempotenz: er macht aus einem erneuten
        // Subscriben einen Upsert, selbst wenn zwei POSTs sich ueberholen. Die
        // Endpoint-Pruefung im Endpunkt ist nur der schnelle Weg dorthin.
        //
        // ascii statt utf8mb4: ein Push-Endpoint ist eine URL aus ASCII-Zeichen.
        // Das haelt den 512er-Indexschluessel innerhalb der MySQL-Grenze fuer
        // Indexlaengen, wo utf8mb4 (4 Byte/Zeichen) ihn sprengen wuerde.
        modelBuilder.Entity<PushSubscription>()
            .Property(s => s.Endpoint).HasCharSet("ascii");
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(s => s.Endpoint).IsUnique().HasDatabaseName("IX_PushSubscription_Endpoint");

        // Der Primaerschluessel des Entdopplungs-Merkers IST die Behandlungs-ID -
        // je Behandlung genau eine Zeile. Er wird beim Versand explizit gesetzt,
        // NICHT von der Datenbank vergeben. Ohne ValueGeneratedNever hielte EF
        // die int-PK fuer eine Identity-Spalte und ignorierte den gesetzten Wert
        // beim Insert - der Merker landete unter einer fremden ID und der zweite
        // Lauf faende ihn nie wieder.
        modelBuilder.Entity<PushReminderLog>()
            .Property(l => l.ClawTreatmentId).ValueGeneratedNever();

        // Ein Durchlauf = hoechstens ein Abbruch. Der Durchlauf haengt an der
        // ClientId seiner ERSTEN Gabe, und die Eindeutigkeit ist auch hier die
        // eigentliche Garantie der Idempotenz: ein wiederholtes POST wird zum
        // No-op, egal wie oft der Nutzer tippt oder die Leitung abreisst. Die
        // Pruefung im Dienst ist nur der schnelle Weg dorthin.
        //
        // ascii statt utf8mb4 aus demselben Grund wie bei den vier
        // Behandlungstabellen: eine GUID besteht aus Hexziffern und
        // Bindestrichen.
        modelBuilder.Entity<TreatmentPlanCancellation>()
            .Property(c => c.StartTreatmentClientId).HasColumnType("char(36)").HasCharSet("ascii");
        modelBuilder.Entity<TreatmentPlanCancellation>()
            .HasIndex(c => c.StartTreatmentClientId)
            .IsUnique()
            .HasDatabaseName("IX_Treatment_Plan_Cancellation_Start_Treatment_Client_Id");

        // Ein Tag, hoechstens eine Sammel-Push. Der Index ist der harte Riegel
        // hinter der Regel in PushLogic.ShouldSendReminderToday: wollten zwei
        // Instanzen gleichzeitig senden, scheitert die zweite beim Insert.
        modelBuilder.Entity<TreatmentPlanPushLog>()
            .HasIndex(l => l.SentOn)
            .IsUnique()
            .HasDatabaseName("IX_Treatment_Plan_Push_Log_Sent_On");
    }
}
