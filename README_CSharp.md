# HIE Quota MMC 3.0 Snap-In (C#)

## Projektübersicht
Dieses Projekt ist ein MMC 3.0 Property Sheet Extension Snap-In, geschrieben in C# (.NET Framework 4.8).
Es erweitert das "Active Directory-Benutzer und -Computer" (ADUC) Snap-In, um die 3 benutzerdefinierten HIE-Quota-Attribute editierbar zu machen.

## Voraussetzungen
- Windows Server (oder Windows 10/11 mit RSAT)
- .NET Framework 4.8 SDK
- Visual Studio 2019+ (oder höher)
- Die `Microsoft.ManagementConsole.dll` Assembly (liegt im GAC oder im .NET Framework Verzeichnis)

## Build
1. Öffne die Projektmappe in Visual Studio.
2. Stelle sicher, dass die Referenz auf `Microsoft.ManagementConsole.dll` korrekt aufgelöst wird. (Gegebenenfalls Pfad in der `.csproj` anpassen, standardmäßig ist `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` hinterlegt).
3. Kompiliere das Projekt im `Release`-Modus. Die Ausgabe ist `HieQuotaMMC.dll`.

## WICHTIG: .NET Runtime Konfiguration für MMC
Da `mmc.exe` ein 64-Bit (oder 32-Bit) unmanaged Prozess ist, lädt es standardmäßig .NET 3.5 oder die neueste installierte Runtime. Um sicherzustellen, dass .NET 4.8 verwendet wird und keine Konflikte mit anderen Managed Snap-Ins (wie Exchange) auftreten, **muss** eine `mmc.exe.config` Datei erstellt werden:

**Für 64-Bit Systeme (Standard):**
Speichere folgende Datei als `C:\Windows\System32\mmc.exe.config`:

