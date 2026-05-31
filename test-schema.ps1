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
        if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
            Set-ADUser -Identity $TestUser -Server $DomainController -Add @{HIEprohibitsendquota=1024; HIEprohibitreceivequota=2048; HIEstoragequotalimit=4096}
        } else {
            Set-ADUser -Identity $TestUser -Add @{HIEprohibitsendquota=1024; HIEprohibitreceivequota=2048; HIEstoragequotalimit=4096}
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
