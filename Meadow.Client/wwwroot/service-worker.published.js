// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
// Alles unter /api/ geht am Service Worker VORBEI: kein respondWith, also keine
// Cache-Abfrage, kein Cache-Eintrag und kein index.html-Fallback. Der Cache
// haelt ausschliesslich die App-Huelle; die Daten gehoeren der Anwendung.
//
// Zwei konkurrierende Caches waeren die Hauptquelle fuer "warum sehe ich alte
// Daten" - einer im Service Worker, einer in der App, und keiner weiss vom
// anderen.
//
// URL(...).pathname statt url.includes('/api/'): "includes" traefe auch
// https://host/app?ziel=/api/x. toLowerCase(), weil die Routen dieser App
// gemischt geschrieben sind (/Kuh_Daten).
function isApiRequest(request) {
    return new URL(request.url, self.location.origin)
        .pathname.toLowerCase().startsWith('/api/');
}

self.addEventListener('fetch', event => {
    if (isApiRequest(event.request)) { return; }
    event.respondWith(onFetch(event));
});

// Web-Push-Handler — der Kern von Task 3.
//
// Das ist die AUSGELIEFERTE SW-Datei (Blazor kopiert ihren Inhalt beim Publish
// nach service-worker.js). Damit Push auch bei geschlossener App eine
// Systembenachrichtigung zeigt, MUESSEN die Handler hier stehen - der Browser
// weckt genau diesen Service Worker, wenn eine Push-Nachricht ankommt. Die
// beiden Funktionen sind wortgleich zu denen im Dev-service-worker.js; sie
// beruehren den Offline-Cache nicht.
self.addEventListener('push', event => event.waitUntil(meadowShowNotification(event)));
self.addEventListener('notificationclick', event => event.waitUntil(meadowOpenWindow(event)));

// Zeigt die Systembenachrichtigung aus dem Payload. Der Server schickt
// { title, body, url } als JSON; faellt das Auspacken aus, bleibt eine neutrale
// Meldung, damit userVisibleOnly nie verletzt wird - ein still verschluckter
// push kostet die Push-Berechtigung.
async function meadowShowNotification(event) {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch (e) {
        data = { body: event.data ? event.data.text() : '' };
    }

    const title = data.title || 'Meadow';
    const options = {
        body: data.body || '',
        icon: 'icon-192.png',
        badge: 'icon-192.png',
        data: { url: data.url || '/' }
    };

    await self.registration.showNotification(title, options);
}

// Fokussiert ein offenes Fenster oder oeffnet eines auf data.url.
async function meadowOpenWindow(event) {
    event.notification.close();
    const target = (event.notification.data && event.notification.data.url) || '/';

    const clientList = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of clientList) {
        if ('focus' in client) {
            client.navigate(target);
            return client.focus();
        }
    }

    if (self.clients.openWindow) {
        return self.clients.openWindow(target);
    }
}

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/,
    /\.woff$/,
    // NEU. Die Vorlage hat nur /\.woff$/ - wegen des $ trifft das
    // "barlow-400.woff2" NICHT. Ohne diese Zeile faellt die App offline auf
    // eine Systemschrift zurueck, und weil online alles stimmt, faellt es beim
    // Testen nie auf.
    /\.woff2$/,
    /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/
];

// Was NICHT vorab geladen wird. Ausgeliefert wird es weiterhin - es fehlt nur
// im Offline-Cache, und online laedt es unveraendert nach.
const offlineAssetsExclude = [
    /^service-worker\.js$/,

    // Die zwoelf Mockups der Landing-Page, 4,4 MB. Wer die App auf dem Telefon
    // installiert hat, sieht diese Seite nie wieder.
    /^images\/Mockups\//,
    /^images\/hero\.jpg$/,

    // Ace-Editor, 2,6 MB nach dem Trimmen, fuer ein Feld, das ausschliesslich
    // im Kennzahl-Dialog vorkommt. Offline bleibt genau dieses eine Feld leer;
    // Kennzahlen zu bearbeiten ist ohnehin Schreibtischarbeit und steht auf der
    // Online-only-Liste.
    /^_content\/Blazor\.AceEditorJs\//,

    // Die Homescreen-Icons liest das BETRIEBSSYSTEM aus dem Manifest, beim
    // Installieren, online. Die laufende App zeigt sie nirgends - im
    // Offline-Cache waeren sie 177 KB fuer nichts.
    /^icon-192.png$/, /^icon-512.png$/,

    // Falls je ein Debug-Publish durchrutscht: ein Satz .pdb waere rund 3 MB.
    /\.pdb$/, /\.map$/
];


// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}
