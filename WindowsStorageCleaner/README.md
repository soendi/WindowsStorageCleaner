# Windows Storage Cleaner

Professionelle Windows-Speicherplatzbereinigung.

## Systemvoraussetzungen

- **Betriebssystem:** Windows 10 / Windows 11 (64-Bit)
- **Administratorrechte:** Erforderlich für alle Bereinigungsfunktionen
- **.NET-Version (nur Framework-abhängige Version):** .NET 8.0 Desktop Runtime

## Installation

### Variante 1: MSI Installer (empfohlen)

1. `WindowsStorageCleaner_Setup.msi` ausführen
2. Installationsassistent folgen
3. Programm wird im Startmenü installiert
4. Starten über "Windows Storage Cleaner"

### Variante 2: Portable ZIP

1. `WindowsStorageCleaner_Portable.zip` entpacken
2. `WindowsStorageCleaner.exe` ausführen
3. Administratorrechte bestätigen

## Funktionsübersicht

### Bereinigungspunkte

| Kategorie | Beschreibung |
|-----------|-------------|
| Datenträgerbereinigung | Temporäre Dateien, Cache, Papierkorb uvm. (cleanmgr) |
| Windows Komponenten | DISM StartComponentCleanup / ResetBase |
| Temporäre Dateien | Benutzer- und System-TEMP |
| Windows Update Cache | Stoppt Dienste, löscht Cache, startet Dienste |
| Ruhezustand | Deaktiviert Ruhezustand, löscht hiberfil.sys |
| Browser Cache | Microsoft Edge, Google Chrome, Firefox |
| Weitere | Ereignisprotokolle, Defender, Crash Dumps, CBS Logs |

### Bereinigungsprofile

- **Sicher (empfohlen):** Temporäre Dateien, Cache, Papierkorb
- **Standard:** Zusätzlich DISM und Browser-Cache
- **Gründlich:** Zusätzlich ResetBase und Defender-Cache
- **Maximal:** Zusätzlich Ruhezustand und alte Installationen

### Funktionen

- Automatische Profilempfehlung basierend auf freiem Speicher
- Analyse der erwarteten Speicherfreigabe vor Bereinigung
- Echtzeit-Fortschrittsanzeige mit Live-Log
- Ergebnisse mit Vorher/Nachher-Vergleich
- Dunkles und helles Design
- Einstellungen werden gespeichert (JSON)
- Protokoll als TXT exportierbar
- Abbrechen-Funktion während der Bereinigung

### Nicht rückgängig machbare Aktionen

Folgende Aktionen erfordern eine explizite Bestätigung durch Eingabe von "JA":

- DISM ResetBase
- Ruhezustand deaktivieren
- Alte Windows Installationen entfernen
- Wiederherstellungspunkte löschen

## Sicherheitshinweise

- Die Anwendung startet automatisch mit Administratorrechten (UAC)
- Ohne Administratorrechte werden keine Bereinigungen durchgeführt
- Kritische Aktionen sind durch eine Sicherheitsabfrage geschützt
- Alle Operationen können jederzeit abgebrochen werden

## Technische Details

- **Sprache:** C# 12
- **Framework:** .NET 8.0 (Windows)
- **GUI:** WPF mit MVVM-Architektur
- **Installer:** MSI (WiX Toolset)
- **Build:** Visual Studio 2022 / .NET SDK 8.0

## Projektstruktur

```
WindowsStorageCleaner/
├── WindowsStorageCleaner/          # Hauptprojekt
│   ├── Models/                     # Datenmodelle
│   ├── ViewModels/                 # MVVM-ViewModels
│   ├── Views/                      # WPF-Oberflächen
│   ├── Services/                   # Geschäftslogik
│   ├── Converters/                 # Wertkonverter
│   ├── Helpers/                    # Hilfsklassen
│   └── Resources/                  # Themes und Ressourcen
├── Installer/                      # MSI-Installer (WiX)
│   └── Product.wxs                 # WiX-Produktdefinition
├── Publish/                        # Self-Contained Build
└── README.md
```

## Build-Anleitung

```bash
# Projekt erstellen
cd WindowsStorageCleaner
dotnet build -c Release

# Self-Contained veröffentlichen
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Publish

# MSI Installer
cd Installer
wix build Product.wxs -o ..\WindowsStorageCleaner_Setup.msi -arch x64
```

## Versionshistorie

**v1.0.0** - Initiale Version

- Vollständige Datenträgerbereinigung
- MVVM-Architektur
- 4 Bereinigungsprofile
- Dunkles/Helles Design
- MSI Installer
- Portable Version
