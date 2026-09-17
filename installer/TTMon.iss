; Skrypt Inno Setup dla TTMon (TTMon.exe)
;
; JAK UZYC:
; 1. Zainstaluj Inno Setup (darmowy): https://jrsoftware.org/isdl.php (aktualnie wersja 7.x)
; 2. Zbuduj appke w trybie Release, samodzielnie (self-contained), zeby user
;    NIE musial miec zainstalowanego .NET 8 Desktop Runtime:
;      dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
;    Wynik pojawi sie w: bin\Release\net8.0-windows\win-x64\publish\
; 3. (Opcjonalnie, jesli podpisujesz) Podpisz TTMon.exe PRZED krokiem 4 -
;    zobacz scripts\sign-exe.ps1 i sekcje "Podpisywanie .exe" w README. Wtedy
;    podpisany plik trafi do instalatora automatycznie (Inno Setup pakuje to,
;    co jest w folderze publish w momencie kompilacji skryptu).
; 4. Otworz ten plik w Inno Setup Compiler (albo `iscc TTMon.iss` z linii komend)
;    - jesli sciezka publish rozni sie od domyslnej ponizej, popraw SourceDir w [Files]
; 5. Compile (Ctrl+F9) - wynik: installer\Output\TTMon_Setup.exe
;
; PODPISANIE SAMEGO PLIKU SETUP.EXE (opcjonalne, osobno od podpisania TTMon.exe
; w srodku): Inno Setup ma dyrektywe SignTool w [Setup], ale wymaga jednorazowej
; konfiguracji w GUI: Tools -> Configure Sign Tools -> dodaj nazwane polecenie
; np. "mytool" wskazujace na signtool.exe + Twoj certyfikat, potem odkomentuj
; linijke "SignTool=mytool" ponizej. Bez tej konfiguracji linijka wywali
; kompilacje - zostawiona zakomentowana domyslnie.

#define MyAppName "TTMon"
#define MyAppVersion "0.12-beta"
#define MyAppPublisher "Blaoyne"
#define MyAppExeName "TTMon.exe"

[Setup]
AppId={{B4A1E2C0-7F3A-4E9B-9C1D-TTMON00003A}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename={#MyAppName}_Setup
Compression=lzma
SolidCompression=yes
; Appka wymaga administratora do odczytu sensorow (LibreHardwareMonitorLib) -
; instalator tez wymaga admina, zeby moc zainstalowac do Program Files
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
; Odkomentuj po jednorazowej konfiguracji w Tools -> Configure Sign Tools:
;SignTool=mytool

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Zaklada wynik `dotnet publish` opisany w naglowku - podmien sciezke jesli
; budujesz inaczej (np. framework-dependent zamiast self-contained)
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Utworz skrot na pulpicie"; GroupDescription: "Dodatkowe skroty:"; Flags: unchecked

[Run]
; shellexec jest KLUCZOWE: bez tego Inno Setup uruchamia plik przez CreateProcess,
; ktore NIE honoruje requireAdministrator w manifescie (dokladnie ten sam
; problem co z debuggerem VS Code - patrz app.debug.manifest) i konczy sie
; cichym bledem ERROR_ELEVATION_REQUIRED (740) zamiast promptu UAC. shellexec
; wymusza ShellExecute, ktore manifest prawidlowo honoruje.
Filename: "{app}\{#MyAppExeName}"; Description: "Uruchom {#MyAppName} teraz"; Flags: nowait postinstall skipifsilent shellexec
