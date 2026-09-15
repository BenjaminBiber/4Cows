using System.Net;
using System.Text;
using System.Text.Json;
using Meadow.Client.Services;
using Meadow.Shared;
using Meadow.Shared.Models;
using Meadow.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.Data.Tests.Traps;

/// <summary>
/// Falle 2: ImmutableDictionary.Add auf einem Schluessel, den es schon gibt.
///
/// Der Ablauf, den diese Tests festhalten: Ein Dienst legt eine Zeile an, der
/// Server vergibt die Id, und der Dienst schreibt sie in seinen Cache. Kommt
/// die erzeugte Id NICHT beim Aufrufer an, steht dort weiter die 0 - und der
/// ZWEITE Insert schreibt dann auf denselben Schluessel. Add wirft dabei,
/// SetItem nicht.
///
/// Warum das kein akademischer Fall ist: genau so stand es in BBCowDataLibrary
/// an sechs Stellen, und der Absturz kam nicht beim Speichern, sondern beim
/// zweiten Speichern - also erst, wenn der Landwirt zwei Behandlungen
/// hintereinander erfasst hatte.
///
/// Der Test ist absichtlich AM DIENST und nicht an ImmutableDictionary. Dass
/// Add bei doppelten Schluesseln wirft, ist eine Eigenschaft der Klasse und
/// braucht keinen Test; dass unsere Insert-Pfade das nicht ausloesen koennen,
/// schon.
///
/// ROT GESEHEN: mit "_cachedTreatments.Add(...)" statt ".SetItem(...)" in
/// HttpClawTreatmentService.InsertDataAsync scheitert
/// Two_inserts_with_the_same_server_id_do_not_throw mit
/// "System.ArgumentException : An item with the same key has already been added."
/// </summary>
public class CacheKeyCollisionTests
{
    /// <summary>
    /// Eine API, die auf JEDEN POST dieselbe Zeile mit der Id 0 zurueckgibt -
    /// also genau das Verhalten, das die Falle ausloest.
    /// </summary>
    private sealed class ImmerNullHandler : HttpMessageHandler
    {
        private readonly Func<object> _body;

        public ImmerNullHandler(Func<object> body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(_body(), MeadowJson.CreateOptions());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                RequestMessage = request,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private static HttpClient Client(Func<object> body) =>
        new(new ImmerNullHandler(body)) { BaseAddress = new Uri("http://localhost/") };

    /// <summary>
    /// Die Outbox wird in diesen Tests nie erreicht - der erfundene Server
    /// antwortet immer mit 201, es geht also nichts in die Zwischenablage. Sie
    /// muss trotzdem gebaut werden, weil der Dienst sie im Konstruktor nimmt.
    /// Ohne JavaScript meldet der lokale Speicher schlicht "nicht verfuegbar".
    /// </summary>
    private static MeadowOutbox Outbox() =>
        new(new MeadowLocalStore(new KeinJs(), NullLogger<MeadowLocalStore>.Instance),
            new MeadowSyncState(),
            NullLogger<MeadowOutbox>.Instance);

    private sealed class KeinJs : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("Im Test gibt es kein JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new InvalidOperationException("Im Test gibt es kein JavaScript.");
    }

    [Fact]
    public async Task Two_inserts_with_the_same_server_id_do_not_throw()
    {
        // Die API bleibt bei der 0 - sie "vergisst" also, eine Id zu vergeben.
        var svc = new HttpClawTreatmentService(
            Outbox(),
            Client(() => new ClawTreatment { ClawTreatmentId = 0, EarTagNumber = "DE 08 1523 1014" }),
            new DatabaseStatusService(),
            NullLogger<HttpClawTreatmentService>.Instance);

        Assert.True(await svc.InsertDataAsync(new ClawTreatment { EarTagNumber = "DE 08 1523 1014" }));

        // Genau hier stand der Absturz. Die Zusicherung ist "wirft nicht",
        // nicht "macht etwas Sinnvolles": eine API, die keine Ids vergibt, ist
        // kaputt, aber sie darf den Client nicht mitreissen.
        var zweiter = await Record.ExceptionAsync(
            () => svc.InsertDataAsync(new ClawTreatment { EarTagNumber = "DE 08 1523 1042" }));

        Assert.Null(zweiter);
        Assert.Single(svc.Treatments);
    }

    [Fact]
    public async Task An_insert_writes_the_server_id_back_into_the_callers_instance()
    {
        // Die Gegenprobe, und der eigentliche Fix hinter Falle 2: kommt die Id
        // zurueck, kollidiert gar nichts erst.
        var svc = new HttpClawTreatmentService(
            Outbox(),
            Client(() => new ClawTreatment { ClawTreatmentId = 57, EarTagNumber = "DE 08 1523 1014" }),
            new DatabaseStatusService(),
            NullLogger<HttpClawTreatmentService>.Instance);

        var meine = new ClawTreatment { EarTagNumber = "DE 08 1523 1014" };
        await svc.InsertDataAsync(meine);

        // Die eigene Instanz, nicht eine frisch deserialisierte: die Seiten
        // halten genau dieses Objekt.
        Assert.Equal(57, meine.ClawTreatmentId);
        Assert.True(svc.Treatments.ContainsKey(57));
    }

    [Fact]
    public async Task A_refetched_row_replaces_the_cached_one_instead_of_colliding()
    {
        // GetByIDAsync legt bei einem Cache-Fehltreffer nach. Zwei Aufrufe
        // derselben Id kommen beide am ContainsKey vorbei, wenn sie sich
        // ueberlappen - auch dort darf kein Add stehen.
        var svc = new HttpClawTreatmentService(
            Outbox(),
            Client(() => new ClawTreatment { ClawTreatmentId = 12, EarTagNumber = "DE 08 1523 1063" }),
            new DatabaseStatusService(),
            NullLogger<HttpClawTreatmentService>.Instance);

        await svc.GetByIDAsync(12);
        var zweiter = await Record.ExceptionAsync(() => svc.GetByIDAsync(12));

        Assert.Null(zweiter);
        Assert.Single(svc.Treatments);
    }
}
