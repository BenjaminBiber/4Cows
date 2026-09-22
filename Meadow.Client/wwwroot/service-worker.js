// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Web-Push-Handler — auch im Dev noetig.
//
// Diese Datei ist der Service Worker, der im ENTWICKLUNGSBUILD laeuft (im
// Publish ersetzt Blazor ihren Inhalt durch service-worker.published.js). Der
// fetch-Handler oben ist bewusst ein No-op, damit F5 nie eine alte Huelle aus
// dem Cache zieht - aber Web Push braucht seine Handler trotzdem HIER, sonst
// laesst sich der Kanal im Dev gar nicht testen. Die Handler stehen deshalb in
// BEIDEN SW-Dateien wortgleich; sie haengen an nichts aus dem Offline-Cache.
self.addEventListener('push', event => event.waitUntil(meadowShowNotification(event)));
self.addEventListener('notificationclick', event => event.waitUntil(meadowOpenWindow(event)));

// Zeigt die Systembenachrichtigung aus dem Payload. Der Server schickt
// { title, body, url } als JSON; faellt das Auspacken aus (leerer oder kaputter
// Payload), bleibt eine neutrale Meldung, damit userVisibleOnly nie verletzt
// wird - ein still verschluckter push kostet die Push-Berechtigung.
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

// Fokussiert ein offenes Fenster oder oeffnet eines auf data.url. Ein bereits
// offener Tab wird bevorzugt, damit nicht bei jedem Klick ein neuer entsteht.
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
