using System.Collections.Immutable;
using BB_Cow.Class;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace BB_Cow.Services;

public class UdderService
{
    private ImmutableDictionary<int, Udder> _cachedUdder = ImmutableDictionary<int, Udder>.Empty;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    private readonly DatabaseStatusService _databaseStatusService;
    public ImmutableDictionary<int, Udder> Udder => _cachedUdder;

    public UdderService(IDbContextFactory<DatabaseContext> contextFactory, DatabaseStatusService databaseStatusService)
    {
        _contextFactory = contextFactory;
        _databaseStatusService = databaseStatusService;
    }

    public async Task GetAllDataAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var udders = await context.Udders.AsNoTracking().ToListAsync();
            _cachedUdder = udders.ToImmutableDictionary(c => c.UdderId);
            _databaseStatusService.ReportSuccess();
            LoggerService.LogInformation(typeof(UdderService), $"Loaded {_cachedUdder.Count} Udder.");
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(UdderService), "Failed to load udder entries, with {@Message}", ex, ex.Message);
        }
    }

    public async Task<bool> InsertDataAsync(Udder udder)
    {
        try
        {
            // UDDER_ID ist eine Identity-Spalte, new Udder() setzt sie aber
            // auf int.MinValue - den Sentinel fuer "unbekannt". EF sieht darin
            // einen ausdruecklich gesetzten Schluessel und schreibt ihn mit;
            // MariaDB nimmt negative Werte in AUTO_INCREMENT-Spalten an.
            //
            // Folgen, solange das nicht auf 0 stand:
            // - Die erste neu gewaehlte Viertel-Kombination landete als Zeile
            //   UDDER_ID = -2147483648. Danach lieferte GetById(int.MinValue)
            //   deren Viertel, statt "keine" - eine Behandlung ohne Auswahl
            //   sah damit aus wie eine mit.
            // - Jede WEITERE neue Kombination scheiterte am doppelten
            //   Primaerschluessel. GetIDByBools gab dann int.MinValue zurueck,
            //   also wieder die Viertel der ersten Zeile: der Nutzer waehlte
            //   LH und bekam gespeichert, was jemand anders zuerst gewaehlt
            //   hatte.
            if (udder.UdderId == int.MinValue)
            {
                udder.UdderId = 0;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Udders.AddAsync(udder);
            var isSuccess = await context.SaveChangesAsync() > 0;
            _databaseStatusService.ReportSuccess();

            if (isSuccess)
            {
                await GetAllDataAsync();
                LoggerService.LogInformation(typeof(UdderService), "Inserted Udder: {@udder}.", udder);
            }

            return isSuccess;
        }
        catch (Exception ex)
        {
            _databaseStatusService.ReportFailure();
            LoggerService.LogError(typeof(UdderService), "Failed to insert udder, with {@Message}", ex, ex.Message);
            return false;
        }
    }

    public async Task<int> GetIDByBools(Udder emptyUdder)
    {
        var id = _cachedUdder.Values.FirstOrDefault(x =>
            x.QuarterLV == emptyUdder.QuarterLV && x.QuarterRV == emptyUdder.QuarterRV &&
            x.QuarterLH == emptyUdder.QuarterLH && x.QuarterRH == emptyUdder.QuarterRH) ?? null;

        if (id == null)
        {
            await InsertDataAsync(emptyUdder);
            return (_cachedUdder.Values.FirstOrDefault(x =>
                x.QuarterLV == emptyUdder.QuarterLV && x.QuarterRV == emptyUdder.QuarterRV &&
                x.QuarterLH == emptyUdder.QuarterLH && x.QuarterRH == emptyUdder.QuarterRH) ?? new Udder()).UdderId;
        }
        else
        {
            return id.UdderId;
        }
    }

    public async Task<int> GetIdForNoQuarters()
    {
        var id = (_cachedUdder.Values
            .FirstOrDefault(x => !x.QuarterLV && !x.QuarterLH && !x.QuarterRH && !x.QuarterRV) ?? new Udder()).UdderId;

        if (id == int.MinValue)
        {
            var newUdder = new Udder
            {
                QuarterLH = false,
                QuarterLV = false,
                QuarterRV = false,
                QuarterRH = false,
            };
            await InsertDataAsync(newUdder);
        }
        return (_cachedUdder.Values
            .FirstOrDefault(x => !x.QuarterLV && !x.QuarterLH && !x.QuarterRH && !x.QuarterRV) ?? new Udder()).UdderId;
    }

    /// <summary>
    /// Ob die ID fuer mindestens ein Viertel steht.
    ///
    /// Unbekannte IDs zaehlen als keines - und dazu gehoert int.MinValue,
    /// also "im Dialog noch nichts gewaehlt". Genau das unterscheidet
    /// "noch nichts gewaehlt" von der Zeile fuer "kein bestimmtes Viertel"
    /// nicht, und muss es auch nicht: beides ist keine Viertel-Angabe.
    /// </summary>
    public bool HasAnyQuarter(int id)
    {
        var udder = GetById(id);
        return udder.QuarterLV || udder.QuarterRV || udder.QuarterLH || udder.QuarterRH;
    }

    public Udder GetById(int id)
    {
        return _cachedUdder.ContainsKey(id) ? _cachedUdder[id] : new Udder();
    }
}
