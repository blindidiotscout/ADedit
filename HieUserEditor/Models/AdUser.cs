using HieUserEditor.ViewModels;
using System.Collections.ObjectModel;

namespace HieUserEditor.Models
{
    /// <summary>
    /// Repräsentiert einen Active Directory Benutzer.
    /// </summary>
    public class AdUser : ViewModelBase
    {
        private string _samAccountName = string.Empty;
        private string _displayName = string.Empty;
        private string _email = string.Empty;
        private bool _enabled;
        private string _distinguishedName = string.Empty;
        private string _ou = string.Empty;
        private ObservableCollection<AdGroup> _memberOf = new();

        public string SamAccountName
        {
            get => _samAccountName;
            set => SetField(ref _samAccountName, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetField(ref _displayName, value);
        }

        public string Email
        {
            get => _email;
            set => SetField(ref _email, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }

        public string DistinguishedName
        {
            get => _distinguishedName;
            set => SetField(ref _distinguishedName, value);
        }

        public string Ou
        {
            get => _ou;
            set => SetField(ref _ou, value);
        }

        public ObservableCollection<AdGroup> MemberOf
        {
            get => _memberOf;
            set => SetField(ref _memberOf, value);
        }
    }
}
