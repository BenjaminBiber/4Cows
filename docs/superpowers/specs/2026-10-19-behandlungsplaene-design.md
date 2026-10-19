# Behandlungspläne

Datum: 2026-10-19
Status: Entwurf freigegeben, Umsetzung offen (Plan: `docs/superpowers/plans/2026-10-19-behandlungsplaene.md`)

## Problem

Viele Medikamente werden nicht einmalig gegeben, sondern als Serie: „3 Gaben innerhalb von
5 Tagen" oder „3 Gaben alle 5 Tage". Heute ist jede Gabe eine einzelne `CowTreatment`-Zeile.
Ob eine Kuh ihre Serie vollständig bekommen hat, wann die nächste Gabe fällig ist und welche
Kühe heute dran sind, muss man sich selbst merken.

Gesucht ist ein Feature **„Behandlungspläne"**:

- pro Medikament ein wiederkehrendes Schema definieren,
- pro Kuh sehen, wie oft sie im laufenden Plan schon behandelt wurde (z.B. „2/3"),
- diese Info auf der Startseite (`/app`) und auf der Kuh-Detailseite (`/Kuh/{CowId}`),
- Benachrichtigungen, wenn eine Gabe fällig oder überfällig ist.

## Entscheidungen

1. **Ein Plan gehört zum Medikament, nicht zur Behandlung.** `Medicine` bekommt die optionale
   Plan-Definition (Anzahl Gaben, Modus, Tage). Pro Medikament genau ein Plan oder keiner.
2. **Zwei Modi.**
   - `Window` – „N Gaben innerhalb von D Tagen". Der Starttag zählt als Tag 1; das Fenster
     endet am Tag `Start + D − 1` (einschließlich). Beispiel: Start Montag, D = 5 → Freitag ist
     der letzte zulässige Tag.
   - `Interval` – „N Gaben alle D Tage". Die nächste Gabe ist fällig am Tag
     **letzte tatsächliche Gabe + D**. Eine verspätete Gabe verschiebt den Rest mit
     (freigegeben: „ab letzter Gabe", nicht fester Raster ab Start).
3. **Der Plan startet automatisch.** Die erste Behandlung mit einem Plan-Medikament, die zu
   keinem offenen Durchlauf gehört, startet einen Durchlauf für diese Kuh. Im Dialog muss
   nichts angeklickt werden.
4. **Durchläufe werden berechnet, nicht gespeichert (Ansatz A).** Eine reine Funktion in
   `Meadow.Shared` leitet die Durchläufe aus den vorhandenen `CowTreatment`-Zeilen ab.
   Gespeichert wird nur zusätzlich, was sich nicht ableiten lässt: der **Abbruch**.
   Begründung: Behandlungen sind offline-schreibbar; ein eigener Plan-Datensatz pro Kuh hätte
   einen fünften offline-schreibbaren Entitätstyp gebraucht (Outbox, `MeadowEntityType`,
   IndexedDB-Store, `IMeadowSyncTarget`, Zuordnung bei Löschungen). So funktioniert der
   automatische Start offline ohne jede Sync-Änderung, und gelöschte oder korrigierte
   Behandlungen sind sofort richtig verrechnet.
5. **Identität eines Durchlaufs** ist die `ClientId` seiner ersten Behandlung. Darauf zeigt
   ein Abbruch.
6. **Manueller Abbruch.** Auf der Kuh-Detailseite gibt es „Plan abbrechen". Ein abgebrochener
   Durchlauf erzeugt keine Erinnerungen mehr und nimmt keine Gaben nach dem Abbruchtag mehr
   an; die nächste Gabe startet einen neuen Durchlauf. Abbrechen ist rückgängig zu machen.
   Abbruch ist **nur online** möglich (wie die Basisdaten).
7. **Stichtag `PlanValidFrom`.** Ohne ihn würde jede historische Gabe eines Medikaments, das
   heute einen Plan bekommt, einen „überfälligen" Durchlauf erzeugen. Der Server setzt
   `PlanValidFrom = heute`, wenn ein Medikament **von „kein Plan" auf „Plan"** wechselt.
   Behandlungen vor diesem Tag zählen nicht. Ändern sich nur die Parameter eines bestehenden
   Plans, bleibt der Stichtag stehen.
8. **Parameter werden nicht eingefroren.** Ändert jemand N, Modus oder D, rechnen auch laufende
   Durchläufe mit den neuen Werten. Wird der Plan am Medikament entfernt, verschwinden dessen
   Durchläufe aus allen Anzeigen. Das ist der bewusste Preis von Ansatz A.
9. **Abgegangene Kühe** (`Cow.IsGone`) und Kühe, die nicht (mehr) in der Kuhliste stehen,
   erscheinen weder auf der Startseite noch in Hinweisen oder Push. Auf ihrer Detailseite
   bleiben die Durchläufe sichtbar.
10. **Benachrichtigungen: Hinweistafel + Push.**
    - Hinweistafel: **ein Hinweis pro fälligem/überfälligem Durchlauf** (wie beim Verband ein
      Hinweis pro Behandlung), Link auf die Kuh-Detailseite, Lasche „Behandlungspläne".
    - Push: **höchstens eine Sammelnachricht pro Tag** an alle Abonnenten, nur wenn etwas heute
      fällig oder überfällig ist, Link auf `/app`.

## Fachliche Regeln (Berechnung)

Eingabe: alle `CowTreatment`, alle `Medicine`, alle `TreatmentPlanCancellation`, `today`.
Pro Paar (Kuh = `CowTreatment.EarTagNumber`, Medikament mit Plan):

1. Behandlungen mit `AdministrationDate.Date >= PlanValidFrom.Date` nehmen, sortiert nach
   `AdministrationDate`, dann `CowTreatmentId` (stabil bei gleichem Zeitpunkt).
2. Durchgehen. Gibt es einen offenen Durchlauf und **nimmt er die Gabe an**, zählt sie dazu.
   Sonst wird der offene Durchlauf geschlossen und die Gabe startet einen neuen.
   - `Window` nimmt an, solange `Datum ≤ Fensterende`.
   - `Interval` nimmt jede spätere Gabe an.
   - Ein abgebrochener Durchlauf nimmt nur Gaben bis einschließlich Abbruchtag an.
3. Erreicht ein Durchlauf N Gaben, ist er sofort `Completed`; die nächste Gabe startet neu.
4. Mehrere Gaben am selben Tag zählen einzeln (manche Präparate werden zweimal täglich gegeben).

Status eines Durchlaufs:

| Status | Bedeutung | aktiv? |
| --- | --- | --- |
| `Completed` | N Gaben erreicht | nein |
| `Cancelled` | manuell abgebrochen (hat Vorrang vor allem außer `Completed`) | nein |
| `Incomplete` | `Window`: Fenster vorbei, N nicht erreicht | nein |
| `Overdue` | `Interval`: heute > nächste Fälligkeit | ja |
| `DueToday` | `Interval`: heute = nächste Fälligkeit. `Window`: heute noch keine Gabe **und** verbleibende Gaben ≥ verbleibende Tage inkl. heute | ja |
| `Upcoming` | sonst, solange offen | ja |

`Window` kennt kein `Overdue`: nach Fensterende ist der Durchlauf `Incomplete`.
`Interval` läuft nicht automatisch aus – dafür gibt es den Abbruch.

## Datenmodell

`Medicine` (Tabelle `Medicine`), alle neu und nullable:

| Property | Spalte | Typ |
| --- | --- | --- |
| `PlanDoseCount` | `Plan_Dose_Count` | `int?` (2–20) |
| `PlanMode` | `Plan_Mode` | `TreatmentPlanMode?` (int: 0 = Window, 1 = Interval) |
| `PlanDays` | `Plan_Days` | `int?` (1–90) |
| `PlanValidFrom` | `Plan_Valid_From` | `DateTime?` (nur Server setzt) |

Ein Plan gilt als definiert, wenn alle drei Parameter gesetzt und gültig sind
(`TreatmentPlanDefinition.IsDefined`).

Neue Tabelle `Treatment_Plan_Cancellation`:

| Property | Spalte | Typ |
| --- | --- | --- |
| `TreatmentPlanCancellationId` | `Treatment_Plan_Cancellation_ID` | int, PK |
| `CowId` | `Cow_ID` | string, required |
| `MedicineId` | `Medicine_ID` | int, required |
| `StartTreatmentClientId` | `Start_Treatment_Client_Id` | Guid, **unique** |
| `CancelledOn` | `Cancelled_On` | DateTime |
| `Reason` | `Reason` | string?, max 190 |

Neue Tabelle `Treatment_Plan_Push_Log` (Entdopplung der täglichen Sammel-Push, getrennt von den
Daten, damit ein Senden keine Datenversion bumpt – wie `PushReminderLog`):

| Property | Spalte | Typ |
| --- | --- | --- |
| `TreatmentPlanPushLogId` | `Treatment_Plan_Push_Log_ID` | int, PK |
| `SentOn` | `Sent_On` | DateTime (Datum), **unique** |

Keine Fremdschlüssel (Projektkonvention seit `InitialCreate`).

## Anzeige

**Basisdaten / Medikament.** `EditMedicineDialog` bekommt einen Abschnitt „Behandlungsplan"
(Schalter „hat Behandlungsplan", Anzahl, Modus „innerhalb von" / „alle", Tage, Vorschautext).
Die Medikamentenliste zeigt eine Spalte „Behandlungsplan" mit `TreatmentPlanDefinition.Describe`,
z.B. „3× innerhalb von 5 Tagen" / „3× alle 5 Tage".

**Kuh-Detailseite.** Neue Lasche „Behandlungspläne" (`?tab=plaene`), Panel
`TreatmentPlansPanel`:
- oben aktive Durchläufe: Medikament, Fortschritt „2/3" mit Balken, Daten der Gaben, nächste
  Fälligkeit bzw. Fensterende, Status-Chip; Knöpfe „Gabe erfassen" (öffnet
  `Add_Cow_Treatment_Dialog` für diese Kuh mit dem Medikament vorbelegt) und „Abbrechen"
  (Bestätigung mit optionalem Grund).
- darunter beendete Durchläufe (abgeschlossen / unvollständig / abgebrochen), neueste zuerst;
  abgebrochene mit „Abbruch zurücknehmen".
- `CowProfile.ActivePlans` speist eine KPI-Kachel „Laufende Pläne", die auf die Lasche verlinkt.

**Startseite.** Neue Karte „Laufende Behandlungspläne" unter der oberen Reihe: alle aktiven
Durchläufe nicht abgegangener Kühe, sortiert Überfällig → Heute fällig → Anstehend, dann nach
Fälligkeit. Zeile: Kuh (Link), Medikament, „2/3", Fälligkeitstext. Ohne aktive Durchläufe
blendet sich die Karte aus.

## Benachrichtigungen

- **Texte** stehen in `Meadow.Shared/Lookups/TreatmentPlanReminderText.cs` – dieselbe Quelle für
  Hinweis und Push.
  - Hinweis `DueToday`: „Kuh {Halsband/Ohrmarke}: Gabe {k}/{N} {Medikament} heute fällig"
  - Hinweis `Overdue`: „Kuh …: Gabe {k}/{N} {Medikament} seit {t} Tag(en) überfällig"
  - Push-Titel „Behandlungspläne", Text z.B. „3 Kühe heute fällig, 1 überfällig"
- **Hinweistafel:** `TreatmentPlanNoticeProvider` (Client), Id `treatment-plan-{StartClientId}`,
  Severity `Warning` (heute fällig) / `Critical` (überfällig), Aktion „Ansehen" →
  `/Kuh/{CowId}?tab=plaene`. Neuberechnung bei `DataArrived(CowTreatment)` und bei geänderten
  Abbrüchen.
- **Push:** `TreatmentPlanPushBackgroundService` nach Vorbild
  `BandageReminderPushBackgroundService` (PeriodicTimer, Env `TreatmentPlanReminderIntervalHours`,
  Fallback wie dort), Entdopplung über `Treatment_Plan_Push_Log` und das vorhandene
  `PushLogic.ShouldSendReminderToday`.

## Sync / Offline

- Plan-Definition liegt am Medikament → kommt mit den Basisdaten (online, offline per
  Lese-Snapshot wie heute).
- Abbrüche: eigener `DataScope.TreatmentPlans`, Lese-Snapshot in IndexedDB (neuer Store,
  `DB_VERSION` erhöhen), Schreiben nur online.
- Behandlungen: unverändert. Keine Änderung an Outbox, `MeadowEntityType`, `IMeadowSyncTarget`.

## Prüfung

xUnit, reine Logik in `Meadow.Data.Tests`:
- `TreatmentPlanDefinitionTests` – Gültigkeit, `Describe`, `ResolveValidFrom`.
- `TreatmentPlanRunsTests` – beide Modi, verspätete Gabe, Fenster abgelaufen, Neustart nach
  Abschluss, zwei Gaben am selben Tag, Abbruch (inkl. Gabe nach Abbruch), Stichtag, Plan
  entfernt, Sortierung, abgegangene Kühe.
- `TreatmentPlanReminderTextTests`, `CowProfileBuilderTests` (Erweiterung).
- Abnahme in der App mit Demo-Daten.

## Bewusst nicht enthalten

- Intervalle in Stunden („alle 12 h").
- Mehrere Pläne pro Medikament; Plan-Parameter beim Start überschreiben.
- Einfrieren der Plan-Parameter beim Start eines Durchlaufs.
- Automatisches Auslaufen überfälliger `Interval`-Durchläufe (dafür: Abbruch).
- Push pro Kuh; persistente Benachrichtigungsliste.
- Hinweis im Behandlungsdialog („diese Gabe ist 2/3") – guter Folgekandidat.
- Wartezeiten.
