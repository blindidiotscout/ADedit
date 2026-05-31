using System;
using System.Collections.Generic;
using Microsoft.ManagementConsole;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HieQuotaMMC
{
    // Hauptklasse für die MMC Snap-In Registrierung
    [SnapInSettings("{B1A2C3D4-E5F6-7890-A1B2-C3D4E5F6A7B8}", 
        DisplayName = "HIE Quota Extension", 
        Description = "Ermöglicht die Bearbeitung der HIE Quota-Attribute im ADUC")]
    public class HieQuotaSnapIn : PropertySheetExtension
    {
        protected override void OnInitialize()
        {
            base.OnInitialize();
            // Registriere die Extension für den ADUC User-Knoten
            // Die GUID {bf967aba-0de6-11d0-a285-00aa003049e30} repräsentiert die AD User Klasse
            this.EnabledTypes.Add(typeof(HieQuotaPropertyPage));
            this.ExtensibleNodeTypes.Add(new Guid("{bf967aba-0de6-11d0-a285-00aa003049e30}"));
        }
    }

    // Property Page, die die UI steuert
    public class HieQuotaPropertyPage : PropertyPage
    {
        private QuotaControl _quotaControl;
        private DirectoryEntry _directoryEntry;
        private string _adsPath;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _quotaControl = new QuotaControl();
            this.Control = _quotaControl;

            // Versuche den ADsPath aus dem SelectionData-Objekt zu extrahieren
            // Dies ist in C# MMC Extensions oft der schwierigste Teil, da ADUC unmanaged ist.
            try
            {
                if (this.SelectionData != null && this.SelectionData.Object != null)
                {
                    // In manchen MMC-Umgebungen liefert SelectionData.Object das COM-Objekt direkt
                    var adObject = this.SelectionData.Object as DirectoryEntry;
                    if (adObject != null)
                    {
                        _directoryEntry = adObject;
                        _adsPath = _directoryEntry.Path;
                    }
                }
                
                // Fallback: Wenn wir kein DirectoryEntry haben, versuchen wir den ADsPath aus den SelectionData.Properties zu lesen
                if (_directoryEntry == null && this.SelectionData != null)
                {
                    // ADUC übergibt den ADsPath oft als String in den SelectionData-Eigenschaften
                    // oder wir müssen ihn über den Namen und den aktuellen DC suchen
                    string userName = this.SelectionData.DisplayName; // Fallback-Name
                    if (!string.IsNullOrEmpty(userName))
                    {
                        DirectoryServiceHelper.Log($"Versuche User '{userName}' über LDAP zu finden.");
                        _directoryEntry = DirectoryServiceHelper.FindUserByName(userName);
                        if (_directoryEntry != null)
                        {
                            _adsPath = _directoryEntry.Path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DirectoryServiceHelper.Log("Fehler beim Initialisieren des AD-Objekts: " + ex.Message);
            }
        }

        protected override void OnApply()
        {
            if (_directoryEntry == null)
            {
                MessageBox.Show("Kein AD-Objekt geladen. Änderungen können nicht gespeichert werden.", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                bool success = DirectoryServiceHelper.SaveQuotas(
                    _directoryEntry,
                    _quotaControl.SendQuotaText,
                    _quotaControl.ReceiveQuotaText,
                    _quotaControl.StorageQuotaText
                );

                if (success)
                {
                    this.Dirty = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Speichern: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DirectoryServiceHelper.Log("Fehler beim Speichern: " + ex.ToString());
            }
        }

        protected override void OnSetActive()
        {
            base.OnSetActive();
            if (_directoryEntry == null)
            {
                _quotaControl.ClearFields();
                MessageBox.Show("Konnte keine Verbindung zum AD-Objekt herstellen. Sind die HIE-Attribute im Schema vorhanden?", "Warnung", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var quotas = DirectoryServiceHelper.LoadQuotas(_directoryEntry);
                _quotaControl.SendQuotaText = quotas.SendQuota;
                _quotaControl.ReceiveQuotaText = quotas.ReceiveQuota;
                _quotaControl.StorageQuotaText = quotas.StorageQuota;

                _quotaControl.ExSendQuotaText = quotas.ExSendQuota;
                _quotaControl.ExReceiveQuotaText = quotas.ExReceiveQuota;
                _quotaControl.ExStorageQuotaText = quotas.ExStorageQuota;

                this.Dirty = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Laden der Attribute: " + ex.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DirectoryServiceHelper.Log("Fehler beim Laden: " + ex.ToString());
            }
        }
    }
}
