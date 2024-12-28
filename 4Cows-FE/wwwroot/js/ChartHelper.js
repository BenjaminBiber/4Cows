let charts = {};

function destroyExistingChart(canvasId) {
    if (charts[canvasId]) {
        charts[canvasId].destroy();
        charts[canvasId] = null;
    }
}

function renderChart(canvasId, config) {
    destroyExistingChart(canvasId); // Sicherstellen, dass kein alter Chart existiert
    const ctx = document.getElementById(canvasId).getContext('2d');
    charts[canvasId] = new Chart(ctx, config);
}