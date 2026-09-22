// Meadow — Web-Push-Bruecke fuer den Browser.
//
// Reines Interop: die Berechtigungsabfrage, das Anmelden beim Push-Dienst des
// Browsers und das Abmelden. Was mit der Anmeldung geschieht (an den Server
// posten), entscheidet die C#-Seite in PushService - hier steht nur, was ohne
// den Browser gar nicht ginge.
//
// Alles ist defensiv: aeltere Browser, iOS ohne installierte PWA und der
// private Modus koennen einzelne Bausteine (Notification, pushManager) gar nicht
// haben. Ein fehlender Baustein ist kein Fehler, sondern "Push geht hier nicht"
// - dann liefert die Funktion null/false, und die App laeuft unveraendert
// weiter.
window.meadowPush = {
    // Ob dieser Browser die drei tragenden Bausteine ueberhaupt mitbringt.
    isSupported: function () {
        return ('serviceWorker' in navigator)
            && ('PushManager' in window)
            && ('Notification' in window);
    },

    // Der aktuelle Berechtigungsstand ohne nachzufragen: 'granted' | 'denied' |
    // 'default'. 'unsupported', wenn es Notification gar nicht gibt.
    permission: function () {
        return ('Notification' in window) ? Notification.permission : 'unsupported';
    },

    // Fragt die Berechtigung an. MUSS aus einer Nutzergeste heraus laufen (Klick)
    // - sonst lehnen Browser die Abfrage still ab. Gibt den Stand zurueck.
    requestPermission: async function () {
        if (!('Notification' in window)) {
            return 'unsupported';
        }
        try {
            return await Notification.requestPermission();
        } catch (e) {
            // Aeltere Safari-Versionen kennen nur die callback-Form. Der
            // try/catch faengt den TypeError, die App laeuft weiter.
            return Notification.permission;
        }
    },

    // Meldet den Browser beim Push-Dienst an und liefert die Anmeldung in genau
    // der Form zurueck, die der Server erwartet: { endpoint, p256dh, auth }.
    // Null, wenn Push nicht geht oder die Berechtigung fehlt.
    //
    // applicationServerKey ist der oeffentliche VAPID-Schluessel (base64url), den
    // C# vom Endpunkt /api/push/public-key geholt hat. Er MUSS als Uint8Array
    // vorliegen - der pushManager akzeptiert die Zeichenkette nicht.
    subscribe: async function (publicKey) {
        if (!this.isSupported() || !publicKey) {
            return null;
        }

        try {
            const registration = await navigator.serviceWorker.ready;

            // Eine vorhandene Anmeldung wiederverwenden: erneutes subscribe mit
            // demselben Schluessel liefert ohnehin denselben Endpoint, aber so
            // sparen wir den Umweg und bleiben auch dann konsistent, wenn der
            // Schluessel sich nicht geaendert hat.
            let subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(publicKey)
                });
            }

            return toServerShape(subscription);
        } catch (e) {
            console.warn('Meadow: Push-Anmeldung fehlgeschlagen.', e);
            return null;
        }
    },

    // Die aktuelle Anmeldung (ohne neu anzumelden), oder null.
    current: async function () {
        if (!this.isSupported()) {
            return null;
        }
        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            return subscription ? toServerShape(subscription) : null;
        } catch (e) {
            return null;
        }
    },

    // Meldet den Browser wieder ab. Gibt den Endpoint der aufgeloesten Anmeldung
    // zurueck, damit C# die passende Zeile serverseitig entfernen kann - oder
    // null, wenn es nichts abzumelden gab.
    unsubscribe: async function () {
        if (!this.isSupported()) {
            return null;
        }
        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                return null;
            }
            const endpoint = subscription.endpoint;
            await subscription.unsubscribe();
            return endpoint;
        } catch (e) {
            console.warn('Meadow: Push-Abmeldung fehlgeschlagen.', e);
            return null;
        }
    }
};

// Zieht endpoint/p256dh/auth aus einer PushSubscription in die flache Form, die
// der Server-Endpunkt liest. Die beiden Schluessel liegen als ArrayBuffer vor
// und muessen base64url-kodiert werden.
function toServerShape(subscription) {
    const json = subscription.toJSON();
    return {
        endpoint: subscription.endpoint,
        p256dh: (json.keys && json.keys.p256dh) || '',
        auth: (json.keys && json.keys.auth) || ''
    };
}

// base64url -> Uint8Array. Der VAPID-Schluessel kommt base64url-kodiert (ohne
// Padding, mit '-'/'_' statt '+'/'/'); der pushManager braucht die rohen Bytes.
// Standardumwandlung aus der Web-Push-Dokumentation.
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; i++) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
