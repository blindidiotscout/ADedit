using System;
using System.DirectoryServices;
using System.IO;

namespace HieQuotaMMC
{
    public class QuotaData
    {
        public string SendQuota { get; set; } = "";
        public string ReceiveQuota { get; set; } = "";
        public string StorageQuota { get; set; } = "";
        
        public string ExSendQuota { get; set; } = "N/A";
        public string ExReceiveQuota { get; set; } = "N/A";
        public string ExStorageQuota { get; set; } = "N/A";
    }

    public static class DirectoryServiceHelper
    {
        private static readonly string LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HieQuotaMMC", "HieQuotaMMC.log");

        public static void Log(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
            }
            catch
            {
                // Falls Logging fehlschlägt (z.B. Berechtigungen), ignorieren wir es.
            }
        }

        public static DirectoryEntry FindUserByName(string userName)
        {
            try
            {
                // Standard-Domain-Context nutzen
                using (DirectoryEntry entry = new DirectoryEntry("LDAP://RootDSE"))
                {
                    string defaultNamingContext = entry.Properties["defaultNamingContext"].Value.ToString();
                    string searchPath = $"LDAP://{defaultNamingContext}";

                    using (DirectoryEntry searchRoot = new DirectoryEntry(searchPath))
                    using (DirectorySearcher searcher = new DirectorySearcher(searchRoot))
                    {
                        searcher.Filter = $"(&(objectClass=user)(sAMAccountName={userName}))";
                        searcher.PropertiesToLoad.Add("ADsPath");
                        
                        SearchResult result = searcher.FindOne();
                        if (result != null)
                        {
                            return result.GetDirectoryEntry();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Fehler bei FindUserByName: " + ex.Message);
            }
            return null;
        }

        public static QuotaData LoadQuotas(DirectoryEntry userEntry)
        {
            QuotaData data = new QuotaData();
            try
            {
                userEntry.RefreshCache(new string[] { 
                    "HIEprohibitsendquota", "HIEprohibitreceivequota", "HIEstoragequotalimit",
                    "mDBOverQuotaLimit", "mDBOverHardQuotaLimit", "mDBStorageQuota" 
                });

                data.SendQuota = GetPropertyValue(userEntry, "HIEprohibitsendquota");
                data.ReceiveQuota = GetPropertyValue(userEntry, "HIEprohibitreceivequota");
                data.StorageQuota = GetPropertyValue(userEntry, "HIEstoragequotalimit");

                data.ExSendQuota = GetPropertyValue(userEntry, "mDBOverQuotaLimit");
                data.ExReceiveQuota = GetPropertyValue(userEntry, "mDBOverHardQuotaLimit");
                data.ExStorageQuota = GetPropertyValue(userEntry, "mDBStorageQuota");
            }
            catch (Exception ex)
            {
                Log("Fehler beim Laden der Quotas: " + ex.Message);
                // Wenn die HIE-Attribute nicht im Schema existieren, wirft RefreshCache eine Exception
                if (ex.Message.Contains("HIEprohibitsendquota") || ex.Message.Contains("Die Verzeichniseigenschaft wurde im Verzeichnis nicht gefunden"))
                {
                    throw new Exception("HIE-Schema-Erweiterung scheint nicht installiert zu sein.");
                }
            }
            return data;
        }

        public static bool SaveQuotas(DirectoryEntry userEntry, string sendQuota, string receiveQuota, string storageQuota)
        {
            try
            {
                // Validierung
                if (!IsValidIntegerOrNull(sendQuota) || !IsValidIntegerOrNull(receiveQuota) || !IsValidIntegerOrNull(storageQuota))
                {
                    throw new ArgumentException("Quota-Werte müssen gültige Ganzzahlen (Integer) oder leer sein.");
                }

                SetPropertyValue(userEntry, "HIEprohibitsendquota", sendQuota);
                SetPropertyValue(userEntry, "HIEprohibitreceivequota", receiveQuota);
                SetPropertyValue(userEntry, "HIEstoragequotalimit", storageQuota);

                userEntry.CommitChanges();
                Log($"Quotas erfolgreich gespeichert für {userEntry.Path}");
                return true;
            }
            catch (Exception ex)
            {
                Log("Fehler beim Speichern der Quotas: " + ex.ToString());
                throw;
            }
        }

        private static string GetPropertyValue(DirectoryEntry entry, string propertyName)
        {
            if (entry.Properties.Contains(propertyName))
            {
                var val = entry.Properties[propertyName].Value;
                return val?.ToString() ?? "";
            }
            return "";
        }

        private static void SetPropertyValue(DirectoryEntry entry, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (entry.Properties.Contains(propertyName))
                {
                    entry.Properties[propertyName].Clear(); // Entfernt den Wert
                }
            }
            else
            {
                entry.Properties[propertyName].Value = int.Parse(value);
            }
        }

        private static bool IsValidIntegerOrNull(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return true;
            return int.TryParse(val, out _);
        }
    }
}
