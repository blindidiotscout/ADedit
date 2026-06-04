# HIE AD Quota Attribute Plugin

## Projekt-Übersicht
Erweiterung eines Active Directory (AD) um 3 benutzerdefinierte Quota-Attribute, 
inklusive MMC-Plugin geschrieben in C# und Stand-Alone PowerShell-Editor.

## Deliverables

### 1. LDIF-Schema-Erweiterung
**Datei:** `hie-schema.ldf`

3 Attribute im Container `CN=HIE-Attributes,CN=Schema,CN=Configuration,DC=...`:

| OID | LDAP-Name | Beschreibung |
|-----|-----------|-------------|
| 1.3.6.1.4.1.65881.1.1.1.1 | HIEprohibitsendquota | Can't send mail limit |
| 1.3.6.1.4.1.65881.1.1.1.2 | HIEprohibitreceivequota | Can't receive mails limit |
| 1.3.6.1.4.1.65881.1.1.1.3 | HIEstoragequotalimit | Max mailbox size limit |

**Anforderungen:**
- Alle 3 Attribute in einem Container zusammengefasst
- Attribute-Typ: Integer (Speicherlimit in MB oder KB)
- Import-Befehl dokumentieren: `ldifde -i -f hie-schema.ldf`

### 2. MMC Snap-In Erweiterung (C)
**Dateien:** 
- `hie_mmc_plugin.c` - Hauptplugin
- `hie_mmc_plugin.h` - Header
- `hie_mmc_plugin.rc` - Ressourcen
- `Makefile` oder Visual Studio Projekt

**Anforderungen:**
- Erweitert die AD Users & Computers MMC Konsole
- Zeigt die 3 HIE-Quota-Attribute im User-Properties Dialog
- Ermöglicht Editieren der Werte
- Zeigt ggf. Exchange-Aliase an (read-only): mDBOverQuotaLimit, mDBOverHardQuotaLimit, mDBStorageQuota
- Fehlerbehandlung für fehlende Schema-Erweiterung

### 3. PowerShell Stand-Alone Editor
**Datei:** `HIE-UserEditor.ps1`

**Anforderungen:**
- GUI oder parametrisierbarer CLI-Modus
- Verbindung zu beliebigem DC (parametrisierbar)
- Editierbare Felder:
  - Name, Titel, Büro, Telefonnummer, Mailadresse
  - Die 3 HIE-Quota-Attribute
  - Aliase/Proxy-Adressen
- LDAP-Bindung mit Credentials (optional)
- Validierung der Eingaben
- Logging der Änderungen

## Mapping: HIE ↔️ Exchange Attribute

| HIE | Exchange | Funktion |
|-----|----------|----------|
| HIEprohibitsendquota | mDBOverQuotaLimit | Sende-Limit |
| HIEprohibitreceivequota | mDBOverHardQuotaLimit | Empfangs-Limit |
| HIEstoragequotalimit | mDBStorageQuota | Mailbox-Max (Warnlevel bei Exchange, aber als Hard-Limit nutzbar) |

## Technische Rahmenbedingungen

- Ziel-Plattform: Windows Server AD
- C-Plugin: VS Build oder MinGW, 64-bit
- PowerShell: PS 5.1+ kompatibel (kein PS7 required)
- LDAP: Standard Windows AD LDAP/ADSI

## Zusätzliche Notes

- Kunden haben KEINEN Exchange Server → daher keine Exchange Schema-Erweiterung
- HIE-Attribute dienen als Ersatz für Exchange-Quota-Funktionalität
- Aliase/Proxy-Adressen sollen editierbar sein (standard AD attribut: proxyAddresses)

## Offene Fragen (optional)

- [ ] Sollen die HIE-Attribute auch in die Global Address List (GAL) sichtbar sein?
- [ ] PowerShell GUI mit WPF/WinForms oder nur CLI?
- [ ] Sollen Änderungen per Email an den User/Admin notifiziert werden?
