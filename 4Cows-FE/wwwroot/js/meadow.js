// Meadow — Theme-Persistenz und Chart-Bruecke.

// Das data-mw-theme-Attribut setzt schon das Inline-Skript im <head> von
// App.razor, damit der erste Paint das richtige Theme hat. Hier steht nur,
// was Blazor zur Laufzeit braucht.
window.meadowTheme = {
    get: function () {
        return document.documentElement.getAttribute('data-mw-theme') || 'light';
    },

    set: function (value) {
        var v = (value === 'dark') ? 'dark' : 'light';
        try { localStorage.setItem('mw-theme', v); } catch (e) { /* privater Modus */ }
        document.documentElement.setAttribute('data-mw-theme', v);
        return v;
    },

    // Liest Meadow-Tokens aus dem CSS. Chart.js braucht konkrete Farbwerte,
    // und so bleiben die Diagramme an derselben Quelle wie der Rest der UI,
    // statt die Hexwerte in C# zu wiederholen.
    // Nimmt sowohl tokens(['a','b']) als auch tokens('a','b'): ein string[]
    // aus C# landet wegen "params object[]" und Array-Kovarianz als einzelne
    // Argumente hier, nicht als ein Array.
    tokens: function () {
        var names = (arguments.length === 1 && Array.isArray(arguments[0]))
            ? arguments[0]
            : Array.prototype.slice.call(arguments);

        var cs = getComputedStyle(document.documentElement);
        var out = {};
        names.forEach(function (n) {
            out[n] = cs.getPropertyValue('--mw-' + n).trim();
        });
        return out;
    }
};

// Duennes Vorschaltstueck vor ChartHelper.js: setzt den Tooltip-Titel auf den
// vollen Monatsnamen, waehrend die Achse nur den Anfangsbuchstaben zeigt (so
// zeichnet es das Design). Eine JS-Funktion laesst sich nicht als Teil eines
// aus C# serialisierten Config-Objekts uebergeben, deshalb hier.
// renderChart / destroyExistingChart aus ChartHelper.js bleiben unveraendert -
// die Chart-Registry dort ist weiter die einzige.
window.meadowChart = {
    render: function (canvasId, config, fullLabels) {
        config.options = config.options || {};
        config.options.plugins = config.options.plugins || {};
        config.options.plugins.tooltip = config.options.plugins.tooltip || {};
        config.options.plugins.tooltip.callbacks = {
            title: function (items) {
                if (!items.length) { return ''; }
                var full = fullLabels && fullLabels[items[0].dataIndex];
                return full || items[0].label;
            }
        };
        window.renderChart(canvasId, config);
    },

    destroy: function (canvasId) {
        window.destroyExistingChart(canvasId);
    }
};
