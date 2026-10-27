---
workflow: motion-graphics
flow: automation
storyboard: no
message: "Meadow dokumentiert Kuh- und Klauenbehandlungen schnell und übersichtlich"
destination: web-autoplay
aspect: 1920x1080
language: de
length: 10s
angle: feature-loop
---

## Intent

Nahtlos loopendes, tonloses Feature-Video (max. 10 s, 30 fps) für Meadow. Drei Funktionen,
je eine Szene: links Textblock (Eyebrow → zweizeilige Headline → Unterzeile → optional Chips),
rechts die echte UI als schwebende Karte. Danach CTA „Jetzt ausprobieren“. Ruhig, hell, hochwertig.

Funktionen (vom Nutzer gewählt): 01 Dashboard · 02 Erfassen (Kuh-Behandlung) · 03 Klaue (Klauenbefund).

## Assets

- assets/logo.png — Meadow-Kuhlogo (aus Meadow.Client/wwwroot), oben links dauerhaft und im CTA.
- assets/ui-*.png — frische Captures aus der laufenden App, deviceScaleFactor 2, nur Dialog/Ansicht.

## Customizations

- Szenenwechsel: alte Karte x −70 / scale 0.97 aus, neue Karte x +90 → 0 ein; Text gleitet leicht nach oben.
- Schritt-Anzeige unten links mit Fortschrittsbalken je Szene; Logo + Name oben links.
- Animierter Cursor (dunkler Pfeil, weiße Kontur, weicher Schatten), power3.inOut, Koordinaten aus
  den Screenshot-Positionen berechnet; Klick = scale 0.82 + Ripple in Markenfarbe; 1–2 Klicks pro Szene.
- CTA: Logo poppt (back.out), Name groß, Claim, Pill-Button; Cursor klickt, Button federt.
- Nahtloser Loop: Frame 0 == letzter Frame; letzte ~0,8 s CTA aus, Szene 1 ein, Cursor zurück.

## Notes

- Nur echte UI, keine erfundenen Funktionen; Seitenverhältnis der Captures nicht verzerren.
- Akzent: Meadow-Petrol statt #16a34a (Nutzerentscheidung). Text #111827, Sekundär #4b5563.
- Schrift Barlow 400/600/700, lokal eingebunden. Karten Radius 16 px, getönter Schatten.
- Kontrast ≥ 4.5:1, Titel ≥ 80 px vom Rand.
- Vor dem Rendern fragen.
