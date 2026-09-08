// Scroll-Reveal fuer die Landing-Page. Wird in App.razor nur eingebunden,
// wenn Demo:Enabled gesetzt ist - ohne Demo gibt es die Seite nicht.
//
// Drei Entscheidungen, die den ganzen Rest erklaeren:
//
// 1. Das Skript setzt data-mw-lp-reveal auf <html>, BEVOR es irgendetwas
//    beobachtet. meadow-landing.css versteckt Reveal-Elemente nur unter
//    diesem Attribut. Faellt das Skript aus - JS deaktiviert, Datei nicht
//    ausgeliefert, Fehler weiter unten -, ist damit nichts unsichtbar.
//
// 2. Ein Wachhund nimmt das Attribut wieder weg, wenn der Beobachter
//    ueberhaupt keine Rueckmeldung gibt. Das ist nicht theoretisch: in
//    einem Tab, der nicht gezeichnet wird (Hintergrund-Rendering,
//    Screenshot-Werkzeuge, aggressives Throttling) fuehrt der Browser
//    keine Intersection-Beobachtung aus und ruft den Callback nie - auch
//    nicht fuer ein Element mitten im Viewport. Ohne Wachhund waere die
//    halbe Seite dann dauerhaft unsichtbar. Normalerweise meldet sich der
//    Beobachter direkt nach observe() fuer jedes Ziel, der Wachhund
//    schlaegt also nur im kaputten Fall an.
//
// 3. Freigeschaltet wird per Attribut data-mw-in, nicht per Klasse. class
//    steht im Blazor-Render-Tree: ein Klick auf einen Anker wie
//    #funktionen laeuft durch den Blazor-Router, und ein anschliessender
//    Re-Render wuerde class auf den Markup-Stand zurueckdiffen und die
//    bereits eingeblendeten Baender wieder ausblenden. Ein Attribut, das
//    Blazor nie gerendert hat, taucht in keinem Diff auf.

(function () {
    'use strict';

    var SELECTOR = '.mw-lp-rv';
    var CURTAIN = 'data-mw-lp-reveal';
    var WATCHDOG_MS = 1500;

    var root = document.documentElement;

    // Vorhang hoch: ab hier ist alles mit .mw-lp-rv unsichtbar, bis es
    // freigeschaltet wird - oder bis der Wachhund den Vorhang wegnimmt.
    function dropCurtain() {
        root.removeAttribute(CURTAIN);
    }

    function start() {
        var nodes = document.querySelectorAll(SELECTOR);

        // Nichts zu beobachten: dann darf auch nichts versteckt sein.
        if (!nodes.length || !('IntersectionObserver' in window)) {
            dropCurtain();
            return;
        }

        var alive = false;
        var watchdog = setTimeout(function () {
            if (!alive) dropCurtain();
        }, WATCHDOG_MS);

        var io = new IntersectionObserver(function (entries) {
            alive = true;
            clearTimeout(watchdog);

            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                entry.target.setAttribute('data-mw-in', '');
                // Einmal eingeblendet bleibt eingeblendet - sonst flackern
                // die Baender beim Zurueckscrollen.
                io.unobserve(entry.target);
            });
        }, {
            // Erst kurz nachdem das Element in den Viewport gelaufen ist,
            // damit die Animation nicht schon am unteren Rand durch ist und
            // man sie gar nicht zu sehen bekommt.
            rootMargin: '0px 0px -12% 0px',
            threshold: 0.1
        });

        nodes.forEach(function (el) { io.observe(el); });
    }

    root.setAttribute(CURTAIN, '');

    // Das Skript liegt am Ende von <body>: waehrend des Parsens ist
    // readyState noch 'loading', das prerenderte Blazor-Markup steht aber
    // erst mit DOMContentLoaded vollstaendig.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
