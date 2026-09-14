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
- Klik lewym na ikonie: wykresy (DetailsForm). Podwojny klik: ustawienia. Prawy klik: menu.
- Autostart z Windows (ustawienia -> Ogolne) - przez Harmonogram Zadan, nie zwykly
  rejestr Run (patrz uwaga nizej)
- Logowanie odczytow do pliku .txt (ustawienia -> Ogolne, domyslnie WYLACZONE) -
  format CSV ze srednikiem jako separator (latwy import do Excela), plik w
  %APPDATA%\TTMon\readings.txt. UWAGA: plik rosnie bez ograniczen, nie ma
  rotacji/limitu rozmiaru - do dorobienia jesli okaze sie potrzebne
- Prawy klik: Ustawienia / Info (branding + wersja + wykryty CPU/GPU/RAM + probna
  lista sensorow plyty glownej) / Zakoncz
- Podwojny klik na ikonie: male okienko z CPU/GPU/WAN, kolorowa kropka przed kazda wartoscia
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
