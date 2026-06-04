Um das Projekt vollständig zu testen, musst du einige Schritte im Active Directory und auf deinem Entwicklungsrechner durchführen. Zuerst müssen wir jedoch einen kleinen Fehler im test-schema.ps1 beheben, der im
vorherigen Code vorhanden war: Es fehlte das Importieren des ActiveDirectory-Moduls, und bei wiederholter Ausführung würde -Add fehlschlagen, wenn die Attribute bereits existieren (daher sollten wir -Replace
verwenden).

Hier ist die korrigierte Version des Test-Skripts und anschließend die Schritt-für-Schritt-Anleitung zum Testen des gesamten Projekts.

test-schema.ps1


<#
.SYNOPSIS
    Testet, ob die HIE-Schema-Erweiterung im Active Directory erfolgreich installiert wurde.

.DESCRIPTION
    Dieses Skript prüft, ob die Attribute HIEprohibitsendquota, HIEprohibitreceivequota
    und HIEstoragequotalimit im AD-Schema existieren und ob sie der Klasse "User" zugeordnet sind.
    Es kann optional auch einen Test-User erstellen und die Werte lesen/schreiben.

.EXAMPLE
    .\test-schema.ps1
    Prüft nur die Schema-Existenz.

.EXAMPLE
    .\test-schema.ps1 -TestUser "testuser01" -DomainController "dc01.local"
    Prüft das Schema und versucht, die Quota-Werte am Testuser zu setzen und zu lesen.
#>

param (
    [string]$TestUser,
    [string]$DomainController
)

# Modul importieren, falls noch nicht geschehen
Import-Module ActiveDirectory -ErrorAction Stop

function Check-SchemaAttribute {
    param ([string]$AttributeName)

    try {
        $schemaPath = (Get-ADRootDSE).schemaNamingContext
        $attr = Get-ADObject -Filter "lDAPDisplayName -eq '$AttributeName'" -SearchBase $schemaPath -Properties * -ErrorAction Stop
        if ($attr) {
            Write-Host "[OK] Attribut '$AttributeName' im Schema gefunden (OID: $($attr.attributeId))." -ForegroundColor Green
            return $true
        } else {
            Write-Host "[FEHLER] Attribut '$AttributeName' nicht im Schema gefunden!" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "[FEHLER] Konnte nicht mit dem Schema verbinden. Ist das ActiveDirectory PowerShell-Modul installiert?" -ForegroundColor Red
        return $false
    }
}

Write-Host "=== Prüfe HIE Schema-Erweiterung ===" -ForegroundColor Cyan

$attr1 = Check-SchemaAttribute -AttributeName "HIEprohibitsendquota"
$attr2 = Check-SchemaAttribute -AttributeName "HIEprohibitreceivequota"
$attr3 = Check-SchemaAttribute -AttributeName "HIEstoragequotalimit"

if (-not ($attr1 -and $attr2 -and $attr3)) {
    Write-Host "`nDie Schema-Erweiterung ist nicht korrekt installiert. Bitte importiere das hie-schema.ldf." -ForegroundColor Yellow
    exit 1
}

Write-Host "`nSchema-Erweiterung ist korrekt installiert!" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($TestUser)) {
    Write-Host "`n=== Teste Lese-/Schreibzugriff auf User '$TestUser' ===" -ForegroundColor Cyan

    try {
        if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
            $user = Get-ADUser -Identity $TestUser -Server $DomainController -Properties HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit -ErrorAction Stop
        } else {
            $user = Get-ADUser -Identity $TestUser -Properties HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit -ErrorAction Stop
        }

        Write-Host "Aktuelle Werte für $TestUser:"
        Write-Host "  HIEprohibitsendquota   : $($user.HIEprohibitsendquota)"
        Write-Host "  HIEprohibitreceivequota: $($user.HIEprohibitreceivequota)"
        Write-Host "  HIEstoragequotalimit   : $($user.HIEstoragequotalimit)"

        Write-Host "`nSetze Testwerte (1024, 2048, 4096)..."
        # WICHTIG: -Replace anstelle von -Add verwenden, da -Add fehlschlägt, falls die Werte bereits existieren!
        if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
            Set-ADUser -Identity $TestUser -Server $DomainController -Replace @{HIEprohibitsendquota=1024; HIEprohibitreceivequota=2048; HIEstoragequotalimit=4096}
        } else {
            Set-ADUser -Identity $TestUser -Replace @{HIEprohibitsendquota=1024; HIEprohibitreceivequota=2048; HIEstoragequotalimit=4096}
        }

        # Werte neu laden
        if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
            $user = Get-ADUser -Identity $TestUser -Server $DomainController -Properties HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit
        } else {
            $user = Get-ADUser -Identity $TestUser -Properties HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit
        }

        if ($user.HIEprohibitsendquota -eq 1024 -and $user.HIEprohibitreceivequota -eq 2048 -and $user.HIEstoragequotalimit -eq 4096) {
            Write-Host "[OK] Werte wurden erfolgreich geschrieben und gelesen!" -ForegroundColor Green

            # Aufräumen
            Write-Host "Bereinige Testwerte..."
            if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
                Set-ADUser -Identity $TestUser -Server $DomainController -Clear HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit
            } else {
                Set-ADUser -Identity $TestUser -Clear HIEprohibitsendquota, HIEprohibitreceivequota, HIEstoragequotalimit
            }
            Write-Host "Testwerte gelöscht." -ForegroundColor Green
        } else {
            Write-Host "[FEHLER] Werte konnten nicht korrekt geschrieben/gelesen werden!" -ForegroundColor Red
        }

    } catch {
        Write-Host "[FEHLER] Konnte User '$TestUser' nicht finden oder beschreiben: $_" -ForegroundColor Red
    }
}


Schritt-für-Schritt Anleitung zum Testen

1. AD-Schema erweitern

Du musst die LDIF-Datei in dein Active Directory importieren. Führe dies auf einem Domain Controller als Administrator aus:


ldifde -i -f hie-schema.ldf -c "DC=X" "DC=deinedomaene,DC=local"


(Ersetze DC=deinedomaene,DC=local durch den echten Root-DN deiner Domäne).

2. Schema prüfen

Führe das korrigierte Skript test-schema.ps1 aus, um zu verifizieren, dass die Attribute im AD angekommen sind:


.\test-schema.ps1


Optional mit einem Test-User:


.\test-schema.ps1 -TestUser "vorhandener_user" -DomainController "dein_dc"


3. C# Projekt kompilieren

 1 Öffne die Projektmappe in Visual Studio.
 2 Stelle sicher, dass die Referenz auf Microsoft.ManagementConsole.dll korrekt aufgelöst wird.
 3 Wechsle in den Release-Modus und kompiliere das Projekt. Die Datei HieQuotaMMC.dll wird erstellt.

4. DLL im GAC registrieren

Die DLL muss im Global Assembly Cache registriert werden, damit die MMC sie findet. Öffne eine Developer Command Prompt für Visual Studio als Administrator:


gacutil /i HieQuotaMMC.dll


5. MMC Snap-In registrieren

Erstelle eine Textdatei namens register.reg mit folgendem Inhalt und führe sie aus (Passe die GUID nicht an, sie muss mit der im C# Code übereinstimmen):


Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MMC\SnapIns\FX:{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}]
"NameString"="HIE Quota Extension"
"About"="{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}"

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MMC\SnapIns\FX:{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}\NodeTypes]
@=""

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MMC\SnapIns\FX:{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}\Standalone]
@=""

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MMC\NodeTypes\{bf967aba-0de6-11d0-a285-00aa003049e30}\Extensions\PropertySheet]
@="{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}"


6. .NET Runtime Konfiguration (Sehr Wichtig!)

Da die MMC ein unmanaged Prozess ist, lädt sie standardmäßig oft nur .NET 3.5. Damit dein .NET 4.8 Code ausgeführt wird, musst du eine Konfigurationsdatei erstellen.

Erstelle eine Datei C:\Windows\System32\mmc.exe.config mit folgendem Inhalt:


<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/>
  </startup>
</configuration>


7. In ADUC testen

 1 Öffne die MMC mit dem ADUC Snap-In (dsa.msc).
 2 Navigiere zu einem Benutzer und öffne die Eigenschaften.
 3 Wenn alles korrekt registriert ist, solltest du nun einen neuen Reiter "HIE Quota Extension" sehen.
 4 Teste das Lesen und Schreiben der Werte.

8. Debugging in Visual Studio (Optional)

Falls der Reiter nicht auftaucht oder Fehler wirft, kannst du dich an den Prozess anhängen:

 1 Öffne die Projekteigenschaften in Visual Studio.
 2 Gehe zu Debug -> Start external program und wähle C:\Windows\System32\mmc.exe.
 3 Als Command line arguments trage dsa.msc ein.
 4 Starte das Projekt im Debug-Modus (F5). Visual Studio startet nun die ADUC-Konsole und du kannst Breakpoints im C# Code setzen, um z.B. zu prüfen, ob OnInitialize oder LoadQuotas aufgerufen werden.


Tokens: 91k sent, 3.5k received.
────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
</configuration>           @="{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}" DirectoryServiceHelper.cs HIE-UserEditor.ps1 HieQuotaMMC.cs HieQuotaMMC.csproj QuotaPropertyPage.cs README.md README_CSharp.md
gacutil /i HieQuotaMMC.dll hie-schema.ldf                             test-schema.ps1
ask>
