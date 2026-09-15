namespace Meadow.Api.BackgroundServices;

/// <summary>
/// Was der Server vom Demo-Modus wissen muss: die Stunde des naechtlichen
/// Zuruecksetzens.
///
/// Eigener Typ statt DemoSettings, weil DemoSettings mit den Komponenten in den
/// Browser gewandert ist und dort ueber MeadowRoutes entscheidet, wohin
/// "Dashboard" zeigt - eine reine Oberflaechenfrage, die auf dem Server nichts
/// zu suchen hat. Die zwei Werte hier doppelt zu halten ist billiger, als
/// Routenwissen in die API zu ziehen.
/// </summary>
public sealed record DemoOptions(bool Enabled, int ResetHour);
