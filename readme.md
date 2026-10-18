# Resort Empire – Paradise Tycoon

Paradise Tycoon je mobilná 2D hra vytvorená v hernom engine Unity. Tento repozitár obsahuje zdrojový projekt hry.

## Použitá verzia Unity

- **Verzia:** Unity 6.3 LTS (6000.3.26f1)
- **Šablóna projektu:** Universal 2D (URP)
- **Cieľová platforma:** Android
- **Orientácia obrazovky:** Na šírku (Landscape)

## Otvorenie projektu

1. Nainštalujte Unity Hub.
2. Nainštalujte Unity 6.3 LTS (6000.3.26f1).
3. Pri inštalácii pridajte moduly:
   - Android Build Support
   - Android SDK & NDK Tools
   - OpenJDK
4. Stiahnite alebo naklonujte tento repozitár.
5. V Unity Hub kliknite na **Add → Add project from disk**.
6. Vyberte priečinok projektu a otvorte ho.

## Postup vytvorenia APK

1. Otvorte projekt v Unity.
2. V hornom menu vyberte **File → Build Profiles**.
3. V zozname platforiem vyberte **Android**.
4. Ak Android nie je aktívny, kliknite na **Switch Platform**.
5. Skontrolujte, či je v zozname scén zahrnutá `Scenes/SampleScene`.
6. Uistite sa, že možnosť **Build App Bundle (Google Play)** nie je zaškrtnutá.
7. Kliknite na **Build**.
8. Vyberte umiestnenie a uložte súbor ako `ParadiseTycoon2D.apk`.
9. Počkajte na dokončenie zostavenia.

## Testovanie APK

1. Preneste vytvorený APK súbor do telefónu s Androidom.
2. Otvorte APK súbor.
3. V prípade potreby povoľte inštaláciu aplikácií z daného zdroja.
4. Nainštalujte a spustite aplikáciu.
5. Overte, či sa na obrazovke zobrazí názov **Paradise Tycoon**.

Aplikáciu je možné nainštalovať bez použitia Google Play.

