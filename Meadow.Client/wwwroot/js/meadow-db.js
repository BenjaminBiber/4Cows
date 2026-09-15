// Meadow - der lokale Speicher. Ein duenner Wrapper um IndexedDB.
//
// Keine Bibliothek und kein EF-auf-SQLite: die App haelt ohnehin VOLLSTAENDIGE
// Tabellen-Schnappschuesse im Arbeitsspeicher. Hier wird nie abgefragt, sondern
// nur eine ganze Tabelle gelesen oder geschrieben. Eine Abfrageschicht waere
// Ladezeit fuer eine Faehigkeit, die niemand aufruft - und Ladezeit ist im
// Browser der teuerste Posten, den diese Phase hat.
//
// Jede Funktion gibt ein Promise zurueck und WIRFT im Fehlerfall. Abgefangen
// wird in MeadowLocalStore.cs; dort steht auch, warum ein Fehler hier die App
// nicht abreissen darf (privater Modus, voller Speicher).

(function () {
    'use strict';

    var DB_NAME = 'meadow';
    var DB_VERSION = 1;

    // Ein Store je Tabelle, plus outbox und meta.
    //
    // Die vier Behandlungstabellen stehen auf clientId und NICHT auf der
    // Server-Id. Drei Gruende, alle drei tragend:
    //
    // 1. clientId existiert, BEVOR die Zeile den Server je gesehen hat. Es gibt
    //    keinen Moment, in dem eine Zeile keinen Schluessel hat - auf der
    //    Server-Id haetten zwei wartende Zeilen beide die 0 und die zweite
    //    ueberschriebe die erste.
    // 2. Nach der Migration hat JEDE Zeile eine, auch die historischen. Server-
    //    zeilen und wartende Zeilen liegen im selben Schluesselraum.
    // 3. Beim Neuabruf UEBERSCHREIBT die Server-Kopie den wartenden Datensatz
    //    (gleicher Schluessel), statt einen zweiten anzulegen. "Zeile steht
    //    kurz doppelt da" kann damit nicht passieren.
    //
    // byServerId steht zusaetzlich daneben, weil Operationen aus der Tabelle
    // mit der Id hereinkommen und nicht mit der clientId.
    var STORES = [
        { name: 'cow', keyPath: 'cowId' },
        { name: 'medicine', keyPath: 'medicineId' },
        { name: 'whereHow', keyPath: 'whereHowId' },
        { name: 'treatmentReason', keyPath: 'treatmentReasonId' },
        { name: 'clawFinding', keyPath: 'clawFindingId' },
        { name: 'udder', keyPath: 'udderId' },
        { name: 'appSetting', keyPath: 'key' },
        { name: 'kpi', keyPath: 'kpiId' },

        { name: 'cowTreatment', keyPath: 'clientId', index: ['byServerId', 'cowTreatmentId'] },
        { name: 'clawTreatment', keyPath: 'clientId', index: ['byServerId', 'clawTreatmentId'] },
        { name: 'plannedCowTreatment', keyPath: 'clientId', index: ['byServerId', 'plannedCowTreatmentId'] },
        { name: 'plannedClawTreatment', keyPath: 'clientId', index: ['byServerId', 'plannedClawTreatmentId'] },

        { name: 'outbox', keyPath: 'seq', autoIncrement: true, index: ['byState', 'state'] },
        { name: 'meta', keyPath: 'key' }
    ];

    // /api/settings liefert ein OBJEKT ({schluessel: wert}) und keine Liste -
    // der Cache dort IST ein Woerterbuch. Genau dieser eine Store wird deshalb
    // beim Schreiben in Zeilen zerlegt und beim Lesen wieder zusammengesetzt.
    // Die Alternative waere ein zweiter Codepfad in C# gewesen, der nur fuer
    // eine Tabelle gilt und den man beim Lesen vergisst.
    var MAP_STORE = 'appSetting';

    // Merkmal einer Zeile, die noch in der Outbox wartet. Bewusst mit
    // Unterstrich: es ist kein Feld des Modells, und C# ignoriert unbekannte
    // Eigenschaften beim Deserialisieren stillschweigend.
    var PENDING = '_pending';

    var dbPromise = null;

    function open() {
        if (dbPromise) { return dbPromise; }

        dbPromise = new Promise(function (resolve, reject) {
            var request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = function () {
                var db = request.result;
                STORES.forEach(function (def) {
                    if (db.objectStoreNames.contains(def.name)) { return; }
                    var store = db.createObjectStore(def.name, {
                        keyPath: def.keyPath,
                        autoIncrement: def.autoIncrement === true
                    });
                    if (def.index) { store.createIndex(def.index[0], def.index[1]); }
                });
            };

            request.onsuccess = function () { resolve(request.result); };
            request.onerror = function () { reject(request.error); };
            request.onblocked = function () { reject(new Error('meadow-db: blocked')); };
        });

        // Ein gescheitertes open darf nicht fuer immer als gescheitert
        // haengenbleiben - der naechste Aufruf soll es erneut versuchen duerfen.
        dbPromise.catch(function () { dbPromise = null; });
        return dbPromise;
    }

    // Eine Transaktion, ein Aufruf. Aufgeloest wird auf oncomplete und nicht
    // auf dem letzten onsuccess: erst dann ist wirklich geschrieben, und
    // genau darauf verlaesst sich die Reihenfolge in OutboxProcessor.
    function tx(name, mode, work) {
        return open().then(function (db) {
            return new Promise(function (resolve, reject) {
                var transaction = db.transaction(name, mode);
                var store = transaction.objectStore(name);
                var result;
                try { result = work(store); } catch (e) { reject(e); return; }
                transaction.oncomplete = function () { resolve(result && result.value !== undefined ? result.value : result); };
                transaction.onerror = function () { reject(transaction.error); };
                transaction.onabort = function () { reject(transaction.error); };
            });
        });
    }

    function readAll(store) {
        var box = {};
        var request = store.getAll();
        request.onsuccess = function () { box.value = request.result || []; };
        return box;
    }

    var api = {
        open: open,

        getAll: function (name) {
            return tx(name, 'readonly', readAll);
        },

        // Als JSON-Zeichenkette, damit der Schnappschuss nicht zweimal durch
        // einen Serialisierer laeuft: C# reicht den Antwortrumpf durch, wie er
        // vom Server kam.
        getAllJson: function (name) {
            return tx(name, 'readonly', readAll).then(function (rows) {
                if (name !== MAP_STORE) { return JSON.stringify(rows); }
                var map = {};
                rows.forEach(function (row) { map[row.key] = row.value; });
                return JSON.stringify(map);
            });
        },

        putAll: function (name, rows) {
            return tx(name, 'readwrite', function (store) {
                rows.forEach(function (row) { store.put(row); });
            });
        },

        put: function (name, row) {
            return tx(name, 'readwrite', function (store) { store.put(row); });
        },

        // Eine einzelne Zeile aus C#, als JSON. pending setzt bzw. entfernt das
        // Wartemerkmal - die Server-Kopie ersetzt die wartende Zeile am selben
        // Schluessel und darf das Merkmal nicht erben.
        putJson: function (name, json, pending) {
            var row = JSON.parse(json);
            if (pending) { row[PENDING] = true; } else { delete row[PENDING]; }
            return api.put(name, row);
        },

        'delete': function (name, key) {
            return tx(name, 'readwrite', function (store) { store['delete'](key); });
        },

        // Ganze Tabelle ersetzen.
        //
        // keepPredicate ist aus JS eine Funktion; aus C# kommt die Zeichenkette
        // 'pending', weil eine Funktion die Interop-Grenze nicht ueberquert -
        // und sie muss INNERHALB der Transaktion laufen, sonst raeumt der
        // Neuabruf eine Zeile weg, die im selben Moment dazukommt.
        replaceAll: function (name, rows, keepPredicate) {
            var keep = keepPredicate === 'pending'
                ? function (row) { return row && row[PENDING] === true; }
                : (typeof keepPredicate === 'function' ? keepPredicate : null);

            return tx(name, 'readwrite', function (store) {
                var request = store.getAll();
                request.onsuccess = function () {
                    (request.result || []).forEach(function (row) {
                        if (keep && keep(row)) { return; }
                        store['delete'](row[storeKeyPath(name)]);
                    });
                    rows.forEach(function (row) { store.put(row); });
                };
            });
        },

        // Der Schnappschuss einer Tabelle, so wie der Server ihn geliefert hat.
        // keepPending nur fuer die vier Behandlungstabellen: dort ueberschreibt
        // die Server-Kopie ihren eigenen Datensatz, und die wartende Zeile
        // bleibt unberuehrt stehen.
        putAllJson: function (name, json, keepPending) {
            var payload = JSON.parse(json);
            var rows = payload;

            if (name === MAP_STORE) {
                rows = Object.keys(payload).map(function (key) {
                    return { key: key, value: payload[key] };
                });
            }

            return api.replaceAll(name, rows, keepPending ? 'pending' : null);
        },

        getMeta: function (key) {
            return tx('meta', 'readonly', function (store) {
                var box = {};
                var request = store.get(key);
                request.onsuccess = function () { box.value = request.result ? request.result.value : null; };
                return box;
            });
        },

        setMeta: function (key, value) {
            return tx('meta', 'readwrite', function (store) { store.put({ key: key, value: value }); });
        },

        // ---- Outbox ------------------------------------------------------

        // seq wird vor dem add ENTFERNT. MeadowJson schreibt jede 0 mit
        // (DefaultIgnoreCondition = Never), und ein mitgeschicktes seq:0 waere
        // fuer IndexedDB ein ausdruecklicher Schluessel - der zweite Eintrag
        // scheiterte dann an genau dieser 0 statt eine 2 zu bekommen.
        outboxAdd: function (json) {
            var entry = JSON.parse(json);
            delete entry.seq;
            return tx('outbox', 'readwrite', function (store) {
                var box = {};
                var request = store.add(entry);
                request.onsuccess = function () { box.value = request.result; };
                return box;
            });
        },

        outboxAllJson: function () {
            return tx('outbox', 'readonly', readAll).then(function (rows) {
                rows.sort(function (a, b) { return a.seq - b.seq; });
                return JSON.stringify(rows);
            });
        },

        outboxPut: function (json) {
            return api.put('outbox', JSON.parse(json));
        },

        outboxDelete: function (seq) {
            return api['delete']('outbox', seq);
        },

        // Der einzige Nutzer des byState-Index: zaehlen, ohne alle Rumpfe zu
        // laden. Die Abarbeitung selbst liest die Eintraege ohnehin komplett.
        outboxCount: function (state) {
            return tx('outbox', 'readonly', function (store) {
                var box = {};
                var request = store.index('byState').count(state);
                request.onsuccess = function () { box.value = request.result; };
                return box;
            });
        },

        // ---- Umgebung ----------------------------------------------------

        isOnline: function () {
            // navigator.onLine ist bekannt optimistisch: "verbunden" heisst nur
            // "eine Netzwerkschnittstelle ist oben". Als AUSLOESER reicht das -
            // ob wirklich jemand antwortet, entscheidet der Versuch selbst.
            return navigator.onLine !== false;
        },

        watchConnectivity: function (ref) {
            window.addEventListener('online', function () {
                ref.invokeMethodAsync('ConnectivitySignal', 'online');
            });

            // Das Tablet im Stall steht die meiste Zeit im Ruhezustand. Ein
            // wiedergezeigter Tab ist der haeufigste Moment, in dem wieder Netz
            // da ist - haeufiger als das online-Ereignis, das der Browser im
            // Schlaf gar nicht erst zustellt.
            document.addEventListener('visibilitychange', function () {
                if (document.visibilityState === 'visible') {
                    ref.invokeMethodAsync('ConnectivitySignal', 'visible');
                }
            });
        }
    };

    // Eigene Variable statt window.meadowDb.x im Rumpf: die Interop-Bruecke
    // ruft eine Funktion nicht zwingend mit dem Elternobjekt als this auf, und
    // ein undefiniertes this faellt erst zur Laufzeit auf - offline, beim
    // Nutzer, in genau dem Moment, fuer den die Datei gebaut wurde.
    window.meadowDb = api;

    function storeKeyPath(name) {
        for (var i = 0; i < STORES.length; i++) {
            if (STORES[i].name === name) { return STORES[i].keyPath; }
        }
        return 'id';
    }
})();
