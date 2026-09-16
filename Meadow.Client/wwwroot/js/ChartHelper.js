let charts = {};
let observers = {};
let redraws = {};

function destroyExistingChart(canvasId) {
    if (observers[canvasId]) {
        observers[canvasId].disconnect();
        observers[canvasId] = null;
    }

    // Auch den entprellten Neuaufbau: ein noch offener Timer haelt die ALTE
    // Konfiguration fest und wuerde die gerade gezeichneten Daten ueberschreiben.
    clearTimeout(redraws[canvasId]);
    redraws[canvasId] = 0;

    if (charts[canvasId]) {
        charts[canvasId].destroy();
        charts[canvasId] = null;
    }
}

function renderChart(canvasId, config) {
    destroyExistingChart(canvasId); // Sicherstellen, dass kein alter Chart existiert
    const canvas = document.getElementById(canvasId);
    charts[canvasId] = new Chart(canvas.getContext('2d'), config);
    watchContainer(canvasId, canvas, config);
}

/* Neu zeichnen, wenn der Kasten um den Canvas seine Hoehe aendert.

   Chart.js hat dafuer eigentlich responsive: true - live nachgemessen tut es
   das hier aber nicht: der Canvas stand auf 545px, waehrend sein Elternteil
   laengst auf 285px geschrumpft war, und blieb es auch nach einem
   ausdruecklichen chart.resize(). Ein Neuaufbau mit derselben Konfiguration
   trifft die Groesse dagegen auf Anhieb - also wird neu aufgebaut.

   Gebraucht wurde das erst, seit ein Diagramm in einer hoehenbegrenzten Spalte
   steht (Kennzahl-Detailseite): auf dem Dashboard ist die Karte so hoch wie
   ihr Canvas, dort gibt es nichts zu beobachten.

   Entprellt ueber einen kurzen Timer: ein Ziehen am Fensterrand feuert sonst
   dutzende Neuaufbauten. Bewusst kein requestAnimationFrame - der Beobachter
   meldet ohnehin nur, wenn die Seite zeichnet; ein zusaetzlicher Frame waere
   eine zweite Bedingung, die in einem verdeckten Tab nie eintritt. */
function watchContainer(canvasId, canvas, config) {
    const box = canvas.parentElement;
    if (!box || typeof ResizeObserver === 'undefined') {
        return;
    }

    let last = Math.round(box.getBoundingClientRect().height);

    const observer = new ResizeObserver(() => {
        const now = Math.round(box.getBoundingClientRect().height);
        // Nur die Hoehe: die Breite behandelt Chart.js von selbst richtig, und
        // eine Reaktion darauf loeste beim Neuaufbau die naechste Meldung aus.
        if (Math.abs(now - last) < 2) {
            return;
        }

        last = now;
        clearTimeout(redraws[canvasId]);
        redraws[canvasId] = setTimeout(() => {
            if (!charts[canvasId] || !canvas.isConnected) {
                return;
            }

            charts[canvasId].destroy();
            charts[canvasId] = new Chart(canvas.getContext('2d'), config);
        }, 80);
    });

    observer.observe(box);
    observers[canvasId] = observer;
}
