namespace Meadow.Client.Components.Ui;

/// <summary>Die Dialogbreiten des Designs.</summary>
public enum MeadowDialogSize
{
    /// <summary>420px — Info, Bestaetigungen, kurze Formulare.</summary>
    Info,

    /// <summary>480px — Kuh-Behandlung.</summary>
    Cow,

    /// <summary>520px — Klauen-Behandlung anzeigen.</summary>
    ShowClaw,

    /// <summary>560px — Klauen-Behandlung hinzufuegen.</summary>
    Claw,

    /// <summary>760px — KPI-Dialog mit SQL-Editor; zeichnet das Design nicht.</summary>
    Wide,

    /// <summary>
    /// 1040px — zwei Spalten: Optionen links, die fertig gerenderte Kachel und ihr SQL rechts.
    ///
    /// Breiter als alles andere, weil hier zwei Dinge NEBENEINANDER stehen muessen: die
    /// Einstellung und ihre Wirkung. Untereinander scrollt das eine aus dem Bild, sobald man am
    /// anderen dreht - und genau das war der Grund, warum man die Kachel vorher nur als abstrakte
    /// Zahl sah statt als das, was hinterher auf dem Dashboard steht.
    /// </summary>
    Split
}
