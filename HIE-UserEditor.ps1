<#
.SYNOPSIS
    HIE AD User Quota Editor (GUI & CLI)

.DESCRIPTION
    Ermöglicht das Bearbeiten von Standard-AD-Attributen und den HIE-Quota-Attributen.
    Verbindung zu beliebigem DC, optionale Authentifizierung, Validierung und Logging.

.PARAMETER DomainController
    Der Ziel-Domain Controller.

.PARAMETER SamAccountName
    Der sAMAccountName des zu bearbeitenden Benutzers (für CLI-Modus).

.PARAMETER Credential
    Optionale PSCredentials für die LDAP-Bindung.

.EXAMPLE
    .\HIE-UserEditor.ps1
    # Startet die GUI

.EXAMPLE
    .\HIE-UserEditor.ps1 -DomainController "dc01.local" -SamAccountName "jdoe"
    # Startet im CLI-Modus
#>

param (
    [string]$DomainController,
    [string]$SamAccountName,
    [System.Management.Automation.PSCredential]$Credential
)

$LogFile = "$PSScriptRoot\HIE-Editor.log"

function Write-Log {
    param ([string]$Message)
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$Timestamp - $Message" | Out-File -FilePath $LogFile -Append
}

function Show-GUI {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $form = New-Object System.Windows.Forms.Form
    $form.Text = "HIE User Editor"
    $form.Size = New-Object System.Drawing.Size(400, 500)
    $form.StartPosition = "CenterScreen"
    $form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $form.MaximizeBox = $false

    # Controls Definition
    $lblDC = New-Object System.Windows.Forms.Label
    $lblDC.Location = New-Object System.Drawing.Point(10, 15)
    $lblDC.Text = "Domain Controller:"
    $form.Controls.Add($lblDC)

    $txtDC = New-Object System.Windows.Forms.TextBox
    $txtDC.Location = New-Object System.Drawing.Point(150, 10)
    $txtDC.Size = New-Object System.Drawing.Size(220, 20)
    if ($DomainController) { $txtDC.Text = $DomainController }
    $form.Controls.Add($txtDC)

    $lblUser = New-Object System.Windows.Forms.Label
    $lblUser.Location = New-Object System.Drawing.Point(10, 45)
    $lblUser.Text = "Benutzer (sAMAccountName):"
    $form.Controls.Add($lblUser)

    $txtUser = New-Object System.Windows.Forms.TextBox
    $txtUser.Location = New-Object System.Drawing.Point(150, 40)
    $txtUser.Size = New-Object System.Drawing.Size(220, 20)
    if ($SamAccountName) { $txtUser.Text = $SamAccountName }
    $form.Controls.Add($txtUser)

    $btnLoad = New-Object System.Windows.Forms.Button
    $btnLoad.Location = New-Object System.Drawing.Point(150, 70)
    $btnLoad.Size = New-Object System.Drawing.Size(100, 30)
    $btnLoad.Text = "Laden"
    $form.Controls.Add($btnLoad)

    # Felder für Attribute
    $yPos = 120
    $fields = @("Name", "Titel", "Büro", "Telefon", "Mailadresse", "HIEprohibitsendquota", "HIEprohibitreceivequota", "HIEstoragequotalimit", "ProxyAdressen")
    $textBoxes = @{}

    foreach ($field in $fields) {
        $lbl = New-Object System.Windows.Forms.Label
        $lbl.Location = New-Object System.Drawing.Point(10, $yPos + 3)
        $lbl.Text = "$field:"
        $lbl.AutoSize = $true
        $form.Controls.Add($lbl)

        $txt = New-Object System.Windows.Forms.TextBox
        $txt.Location = New-Object System.Drawing.Point(150, $yPos)
        $txt.Size = New-Object System.Drawing.Size(220, 20)
        $form.Controls.Add($txt)
        
        $textBoxes[$field] = $txt
        $yPos += 30
    }

    $btnSave = New-Object System.Windows.Forms.Button
    $btnSave.Location = New-Object System.Drawing.Point(150, $yPos + 10)
    $btnSave.Size = New-Object System.Drawing.Size(100, 30)
    $btnSave.Text = "Speichern"
    $form.Controls.Add($btnSave)

    # Load Button Event
    $btnLoad.Add_Click({
        $dc = $txtDC.Text
        $user = $txtUser.Text
        
        if ([string]::IsNullOrWhiteSpace($dc) -or [string]::IsNullOrWhiteSpace($user)) {
            [System.Windows.Forms.MessageBox]::Show("DC und Benutzername müssen ausgefüllt sein.", "Fehler")
            return
        }

        try {
            $searchPath = "LDAP://$dc"
            if ($Credential) {
                $de = New-Object System.DirectoryServices.DirectoryEntry($searchPath, $Credential.UserName, $Credential.GetNetworkCredential().Password)
            } else {
                $de = New-Object System.DirectoryServices.DirectoryEntry($searchPath)
            }
            
            $searcher = New-Object System.DirectoryServices.DirectorySearcher($de)
            $searcher.Filter = "(sAMAccountName=$user)"
            $searcher.PropertiesToLoad.AddRange(@("distinguishedName", "cn", "title", "physicalDeliveryOfficeName", "telephoneNumber", "mail", "HIEprohibitsendquota", "HIEprohibitreceivequota", "HIEstoragequotalimit", "proxyAddresses"))
            
            $result = $searcher.FindOne()
            
            if ($result -eq $null) {
                [System.Windows.Forms.MessageBox]::Show("Benutzer nicht gefunden.", "Fehler")
                return
            }

            $textBoxes["Name"].Text = if ($result.Properties["cn"]) { $result.Properties["cn"][0] } else { "" }
            $textBoxes["Titel"].Text = if ($result.Properties["title"]) { $result.Properties["title"][0] } else { "" }
            $textBoxes["Büro"].Text = if ($result.Properties["physicalDeliveryOfficeName"]) { $result.Properties["physicalDeliveryOfficeName"][0] } else { "" }
            $textBoxes["Telefon"].Text = if ($result.Properties["telephoneNumber"]) { $result.Properties["telephoneNumber"][0] } else { "" }
            $textBoxes["Mailadresse"].Text = if ($result.Properties["mail"]) { $result.Properties["mail"][0] } else { "" }
            $textBoxes["HIEprohibitsendquota"].Text = if ($result.Properties["HIEprohibitsendquota"]) { $result.Properties["HIEprohibitsendquota"][0] } else { "" }
            $textBoxes["HIEprohibitreceivequota"].Text = if ($result.Properties["HIEprohibitreceivequota"]) { $result.Properties["HIEprohibitreceivequota"][0] } else { "" }
            $textBoxes["HIEstoragequotalimit"].Text = if ($result.Properties["HIEstoragequotalimit"]) { $result.Properties["HIEstoragequotalimit"][0] } else { "" }
            
            $proxies = $result.Properties["proxyAddresses"]
            if ($proxies) {
                $textBoxes["ProxyAdressen"].Text = ($proxies -join "; ")
            } else {
                $textBoxes["ProxyAdressen"].Text = ""
            }

            # Speichere DN fürs Speichern
            $script:UserDN = $result.Properties["distinguishedname"][0]
            $script:DC = $dc

        } catch {
            [System.Windows.Forms.MessageBox]::Show("Fehler beim Laden: $_", "Fehler")
            Write-Log "Fehler beim Laden von $user auf $dc : $_"
        }
    })

    # Save Button Event
    $btnSave.Add_Click({
        if ([string]::IsNullOrWhiteSpace($script:UserDN)) {
            [System.Windows.Forms.MessageBox]::Show("Bitte zuerst einen Benutzer laden.", "Fehler")
            return
        }

        # Validierung der Quota-Eingaben (müssen Integer oder leer sein)
        $quotaFields = @("HIEprohibitsendquota", "HIEprohibitreceivequota", "HIEstoragequotalimit")
        foreach ($qf in $quotaFields) {
            $val = $textBoxes[$qf].Text
            if (-not ([string]::IsNullOrWhiteSpace($val)) -and -not ([int]::TryParse($val, [ref]$null))) {
                [System.Windows.Forms.MessageBox]::Show("$qf muss eine Ganzzahl (Integer) sein.", "Validierungsfehler")
                return
            }
        }

        try {
            $userPath = "LDAP://$script:DC/$script:UserDN"
            if ($Credential) {
                $userEntry = New-Object System.DirectoryServices.DirectoryEntry($userPath, $Credential.UserName, $Credential.GetNetworkCredential().Password)
            } else {
                $userEntry = New-Object System.DirectoryServices.DirectoryEntry($userPath)
            }

            $userEntry.Put("title", $textBoxes["Titel"].Text)
            $userEntry.Put("physicalDeliveryOfficeName", $textBoxes["Büro"].Text)
            $userEntry.Put("telephoneNumber", $textBoxes["Telefon"].Text)
            $userEntry.Put("mail", $textBoxes["Mailadresse"].Text)
            
            foreach ($qf in $quotaFields) {
                $val = $textBoxes[$qf].Text
                if ([string]::IsNullOrWhiteSpace($val)) {
                    $userEntry.PutEx(1, $qf, $null) # ADS_PROPERTY_CLEAR
                } else {
                    $userEntry.Put($qf, [int]$val)
                }
            }

            # ProxyAddresses verarbeiten (Semikolon-getrennt)
            $proxyString = $textBoxes["ProxyAdressen"].Text
            $newProxies = $proxyString -split ";" | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            
            $userEntry.PutEx(2, "proxyAddresses", $newProxies) # ADS_PROPERTY_UPDATE

            $userEntry.CommitChanges()
            
            Write-Log "Benutzer $script:UserDN erfolgreich aktualisiert."
            [System.Windows.Forms.MessageBox]::Show("Erfolgreich gespeichert!", "Info")

        } catch {
            [System.Windows.Forms.MessageBox]::Show("Fehler beim Speichern: $_", "Fehler")
            Write-Log "Fehler beim Speichern von $script:UserDN : $_"
        }
    })

    [void]$form.ShowDialog()
}

# Skript Start
if ([string]::IsNullOrWhiteSpace($SamAccountName)) {
    # Kein Benutzername übergeben -> GUI Modus
    Show-GUI
} else {
    # CLI Modus (Vereinfacht für Parameterübergabe, könnte für Batch weiter ausgebaut werden)
    Write-Host "CLI Modus gestartet für Benutzer: $SamAccountName auf DC: $DomainController"
    Write-Log "CLI Modus gestartet für $SamAccountName"
    # Hier könnte die gleiche Logik wie im GUI-Block ohne Forms abgebildet werden.
    # Für den MVP starten wir die GUI, wenn Parameter übergeben wurden, um sofort editierbar zu sein.
    Show-GUI
}
