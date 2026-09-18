# TTMon

## Budowa
```
dotnet restore
dotnet build
```
Wymaga skonfigurowanego zrodla NuGet (`dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org`,
jesli `dotnet nuget list source` pokazuje pusto).

## Uruchomienie
**Release** wymaga uprawnien administratora (app.release.manifest, requireAdministrator) -
LibreHardwareMonitorLib potrzebuje ich do odczytu rejestrow sprzetowych.

**Debug** (F5/Debug Run w VS Code) dziala BEZ administratora (app.debug.manifest,
asInvoker) - sensory CPU/GPU pokaza wtedy "n/a", to oczekiwane. Powod: debugger
uruchamia proces przez CreateProcess, ktore NIE honoruje requireAdministrator w
manifescie (dziala to tylko przez ShellExecute - dwuklik w Eksploratorze, cmd.exe) -
bez tego rozdzielenia F5 konczyloby sie cichym bledem ERROR_ELEVATION_REQUIRED
zamiast faktycznego uruchomienia appki, zmuszajac do recznego dwuklikania .exe i
generujac problemy z zablokowanym plikiem przy kolejnym buildzie. Zeby sprawdzic
prawdziwe odczyty sensorow, zbuduj Release (`dotnet build -c Release`) i uruchom
.exe recznie (Uruchom jako administrator) - lub uruchom cale VS Code jako
administrator, wtedy F5 tez dziala z podniesionymi uprawnieniami od razu.

## Funkcje
- Tray: temperatura CPU, GPU, opoznienie WAN - kolor wg gradientu
- Rozmiar tekstu w ikonie: Maly/Sredni/Duzy (ustawienia -> Czujniki) - z auto-dopasowaniem,
  zeby dluzsze wartosci (np. "104") nigdy sie nie ucinaly
- Wybor producenta: CPU (Auto/Intel/AMD), GPU (Auto/Intel/AMD-Radeon/NVIDIA)
- Czwarta sledzona wartosc: VRM MOS (temperatura z plyty glownej, ten sam
  wspolny gradient co CPU/GPU) - w ikonie priorytet CPU > GPU > VRM > WAN
- Wybor czcionki ikony w trayu (domyslnie Bahnschrift - czytelne cyfry):
  Segoe UI / Bahnschrift (wbudowana w Windows 10/11) / Dosis / JetBrains Mono
  (obie dostarczone przez usera, osadzone w binarce - Resources/Dosis.ttf,
  Resources/JetBrainsMono-Bold.ttf)
- Ustawialne zakresy gradientu osobno dla temperatury (C) i dla WAN (ms)
- Ekran powitalny (splash) przy starcie - fade in/hold/fade out (4s), mozna wylaczyc w ustawieniach
- Klik lewym na ikonie: wykresy (DetailsForm). Prawy klik: menu (Info/Sensory/Ustawienia/Zakoncz).
- Autostart z Windows (ustawienia -> Ogolne) - przez Harmonogram Zadan, nie zwykly
  rejestr Run (patrz uwaga nizej)
- Logowanie odczytow do pliku .csv (ustawienia -> Ogolne, domyslnie WYLACZONE) -
  srednik jako separator, plik w %APPDATA%\TTMon\readings.csv - Excel otwiera go
  natywnie jako tabele przy dwukliku, wykresy robi sie identycznie jak z .xlsx.
  Swiadomie NIE .xlsx - patrz komentarz w ReadingsLogger.cs (ten format trzeba
  by wczytywac+zapisywac caly przy kazdym dopisywanym wierszu, co przy
  dlugotrwalym logowaniu zaczeloby zauwazalnie spowalniac appke). UWAGA: plik
  rosnie bez ograniczen, nie ma rotacji/limitu rozmiaru - do dorobienia jesli
  okaze sie potrzebne
- Kontur wokol cyfr w ikonie (ustawienia -> Czujniki, domyslnie WLACZONY) -
  pomaga na paskach zadan z przezroczystoscia, gdzie same nasycone kolory
  gradientu czasem gina w tle
- Tryb ciemny (ustawienia -> Ogolne) - koloruje Ustawienia/Info/Sensory/wykresy
  na ciemno, wlacznie z ciemnym paskiem tytulowym (DWM, Windows 10 1809+/11) -
  patrz ThemeHelper.cs
- "Sensory" w menu - osobne okno z lista WSZYSTKICH czujnikow plyty glownej
  (temperatury/wentylatory/napiecia), kolumny Wartosc/Min/Max/Sr (z ostatnich
  180s), checkbox "Ukryj nieaktywne" (filtruje czujniki o wartosci 0, np.
  niepodpiete zlacza pompy) + wykres wybranego wiersza na dole, wzorowane na
  zakladce Sensors w GPU-Z (klik wiersz -> wykres pod spodem pokazuje TEGO
  czujnika historie). Kazdy typ ma inna skale, wiec wykres uzywa auto-zakresu
  (SparklineChart.AutoRange), nie sztywnego jak przy CPU/GPU/WAN. Layout okna
  liczony recznie (nie przez Anchor) - patrz komentarz w SensorsForm.cs
- Prawy klik: Info (branding + wersja + wykryty CPU/GPU/RAM) / Sensory / Ustawienia / Zakoncz
- Klik lewym na ikonie otwiera male okienko: CPU/GPU/WAN, kolorowa kropka przed kazda wartoscia
- Czwarta i piata sledzona wartosc: VRM MOS oraz CPU FAN (RPM), obie z toggle w
  ustawieniach i wlasnym wykresem w okienku podgladu (kolejnosc: CPU/GPU/VRM/
  CPU FAN/WAN). Priorytet ikony trayu (bez zmian): CPU > GPU > VRM > WAN
- Kazde okno (Podglad/Info/Sensory/Ustawienia) moze byc otwarte tylko RAZ naraz -
  kolejne klikniecie aktywuje juz otwarte zamiast tworzyc nowe. Wczesniejszy
  blad: ShowDialog() blokuje watek WLASNA zagniezdzona petla komunikatow, ktora
  DALEJ obsluguje klikniecia w tray - kazdy klik w trakcie otwierania otwieral
  kolejne okno wewnatrz poprzedniego, w nieskonczonosc. Naprawione przejsciem
  na niemodalne Show() + pojedyncza referencja per typ okna
  (TrayApplicationContext.ShowOrActivate)
- Podwojny kontur w ikonie (biala "aureola" + czarny pierscien, nie pojedynczy
  czarny jak wczesniej) - na przezroczystym pasku zadan tlo pod ikona bywa raz
  jasne, raz ciemne zaleznie co jest na pulpicie, wiec sam czarny kontur ginal
  na ciemnym tle tak samo jak wypelnienie. Biale+czarne razem gwarantuja
  kontrast wzgledem obu skrajnosci jednoczesnie
- Tooltip na wykresach w tym okienku - najedz mysza, pokaze wartosc i ile sekund
  temu (np. "72.3C - -42s")
- Opcja "zawsze na wierzchu" dla okienka z wykresami (ustawienia -> Ogolne)

## WAZNE ograniczenie: rozmiar ikony w trayu
Fizycznego rozmiaru slotu ikony w trayu NIE da sie zwiekszyc z poziomu aplikacji -
to systemowe ograniczenie Windows, wspolne dla wszystkich aplikacji. "Rozmiar" w
ustawieniach reguluje wiec ile miejsca W OBREBIE tego stalego kwadratu zajmuje
tekst (TrayIconRenderer.cs, z auto-kurczeniem czcionki gdy trzeba).

## Autostart - dlaczego przez Harmonogram Zadan a nie rejestr Run
Zwykly klucz rejestru `HKCU\...\Run` uruchamia program bez podniesionych uprawnien -
poniewaz nasz manifest wymaga administratora, Windows pytalby o zgode UAC PRZY
KAZDYM logowaniu. Zadanie w Harmonogramie z `/RL HIGHEST`, utworzone raz (kiedy
appka juz dziala jako administrator), startuje podniesione bez ponownego pytania.
AutostartManager.cs woła `schtasks.exe` - jesli z jakiegos powodu nie zadziala
(brak uprawnien, zablokowane przez polityke grupowa), ustawienia pokaza komunikat
zamiast cichej porazki.

Samo-naprawa sciezki: przy KAZDYM starcie appki, jesli autostart jest wlaczony,
zadanie jest cicho odswiezane na biezaca sciezke .exe (TrayApplicationContext.cs).
Bez tego przeniesienie/przeinstalowanie programu w innym miejscu zostawialoby
zadanie wskazujace na martwy, stary plik - wygladajace na wlaczone w ustawieniach
(bo zadanie istnieje), ale realnie nic nie uruchamiajace przy logowaniu.

## Instalator (Inno Setup)
W folderze `installer/TTMon.iss` jest gotowy skrypt dla Inno Setup (darmowy,
najpopularniejszy do tego typu aplikacji: https://jrsoftware.org/isdl.php, aktualnie
wersja 7.x). Skrocona instrukcja (pelna w naglowku pliku .iss):
1. `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
2. Zainstaluj Inno Setup, otworz `installer/TTMon.iss`, Compile (Ctrl+F9)
3. Wynik: `installer/Output/TTMon_Setup.exe` - gotowy instalator z odinstalowywaniem,
   skrotem w Menu Start i opcjonalnym skrotem na pulpicie
NIE zostalo to przetestowane na zywo (Inno Setup to Windows-owe GUI, nie mam jak go
tu uruchomic) - jesli sciezka `dotnet publish` wyjdzie inna niz zakladana w skrypcie,
popraw `Source:` w sekcji `[Files]`.

## Podpisywanie .exe (self-signed)
Skrypt: `scripts\sign-exe.ps1`. Uzycie:
```
dotnet build -c Release
.\scripts\sign-exe.ps1 -CertPath "C:\sciezka\do\certyfikatu.pfx"
```
Skrypt sam znajdzie `signtool.exe` (Windows SDK) i zapyta o haslo do `.pfx`
interaktywnie (bezpieczniej niz podawanie w linii komend).

**UCZCIWE ZASTRZEZENIE co samopodpisany certyfikat NAPRAWDE daje:**
- U CIEBIE, po dodaniu certyfikatu do "Zaufane osoby trzecie"/"Zaufani
  wydawcy" (`certmgr.msc` -> Trusted Publishers -> Import), Windows przestanie
  pokazywac ostrzezenie "Nieznany wydawca" przy uruchamianiu
- U KAZDEGO INNEGO usera (jesli bedziesz dystrybuowac .exe/instalator dalej)
  ostrzezenie SmartScreen/UAC "Nieznany wydawca" ZOSTANIE - ich system nie ma
  powodu ufac Twojemu prywatnemu certyfikatowi. Usuniecie tego ostrzezenia u
  wszystkich wymaga prawdziwego certyfikatu od zaufanego CA (patrz rozmowa -
  ~215-320$/rok + token sprzetowy od 2026)
- Podpis potwierdza za to integralnosc (plik nie zostal zmieniony po podpisaniu)
  i jest jednym z sygnalow, ktore antywirusy biora pod uwage przy ocenie
  ryzyka - moze pomoc z fałszywymi alarmami, ale tego nie gwarantuje


- Nazwa/tagline/autor: `AppInfo.cs` (AppName, Tagline, Author)
- Obrazek splash/Info: `Resources/splash.jpg` - osadzony w binarce przy buildzie
  (EmbeddedResource w .csproj), nie kopiowany jako osobny plik obok .exe
- Ikona pliku .exe (widoczna w Eksploratorze/Alt+Tab, INNA niz generowana w locie
  ikona trayu): `Resources/app.ico`, wygenerowana z wycinka splash.jpg (krag
  kabli). Podmien plik i sciezke `<ApplicationIcon>` w .csproj, zeby zmienic
- Czcionki Dosis/JetBrains Mono: juz dostarczone (`Resources/Dosis.ttf`,
  `Resources/JetBrainsMono-Bold.ttf`), osadzone w binarce przy buildzie przez
  warunkowy `EmbeddedResource` w `.csproj` (`Condition="Exists(...)"` - jesli
  ktos usunie plik, build i tak przejdzie, wybor tej czcionki cicho wroci do
  Segoe UI, patrz EmbeddedFontLoader.cs)

## Stan tego szkieletu - NIE ZWERYFIKOWANE NA ZYWO (od strony nowych funkcji z tej tury)
Splash, Info z brandingiem, gradient, DPI awareness i dopasowanie tekstu w ikonie
zostaly potwierdzone dzialajace u usera. Nowe w tej turze - autostart (schtasks),
osadzony jako resource obrazek (zamiast pliku obok), wylaczanie splasha, 3 poziomy
rozmiaru ikony, skrypt instalatora - NIE zostaly jeszcze przetestowane.

## Znane braki / TODO
- SettingsForm ma reczny layout (bez Designer.cs)
