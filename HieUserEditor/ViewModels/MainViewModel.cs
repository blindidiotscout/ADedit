using HieUserEditor.Models;
using HieUserEditor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HieUserEditor.ViewModels
{
    /// <summary>
    /// Hauptansichtmodell für den HIE Benutzer-Editor.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IActiveDirectoryService _adService;
        private CancellationTokenSource? _cts;

        private string _searchTerm = string.Empty;
        private SearchType _searchType = SearchType.Name;
        private AdUser? _selectedUser;
        private AdGroup? _selectedMemberGroup;
        private AdGroup? _selectedAvailableGroup;
        private bool _isBusy;
        private string _statusMessage = "Bereit";

        /// <summary>
        /// Standardkonstruktor für XAML-Instanziierung.
        /// Erwartet, dass die Verbindungseinstellungen über den Konfigurationsdialog angepasst werden.
        /// </summary>
        public MainViewModel()
            : this(new ActiveDirectoryService(Environment.UserDomainName ?? "default"))
        {
        }

        public MainViewModel(IActiveDirectoryService adService)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));

            Users = new ObservableCollection<AdUser>();
            AvailableGroups = new ObservableCollection<AdGroup>();
            SearchTypes = Enum.GetValues<SearchType>();

            SearchCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SearchTerm));
            ListAllCommand = new RelayCommand(async _ => await ListAllAsync(), _ => !IsBusy);
            CancelCommand = new RelayCommand(_ => Cancel(), _ => IsBusy);
            EnableUserCommand = new RelayCommand(async _ => await EnableUserAsync(), _ => !IsBusy && SelectedUser != null && !SelectedUser.Enabled);
            DisableUserCommand = new RelayCommand(async _ => await DisableUserAsync(), _ => !IsBusy && SelectedUser != null && SelectedUser.Enabled);
            ResetPasswordCommand = new RelayCommand(async _ => await ResetPasswordAsync(), _ => !IsBusy && SelectedUser != null);
            AddToGroupCommand = new RelayCommand(async _ => await AddToGroupAsync(), _ => !IsBusy && SelectedUser != null && SelectedAvailableGroup != null);
            RemoveFromGroupCommand = new RelayCommand(async _ => await RemoveFromGroupAsync(), _ => !IsBusy && SelectedUser != null && SelectedMemberGroup != null);
            ShowSettingsCommand = new RelayCommand(_ => ShowSettings());
        }

        public ObservableCollection<AdUser> Users { get; }
        public ObservableCollection<AdGroup> AvailableGroups { get; }
        public IEnumerable<SearchType> SearchTypes { get; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetField(ref _searchTerm, value))
                {
                    SearchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public SearchType SearchType
        {
            get => _searchType;
            set => SetField(ref _searchType, value);
        }

        public AdUser? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetField(ref _selectedUser, value) && value != null)
                {
                    _ = LoadUserGroupsAsync();
                    EnableUserCommand.RaiseCanExecuteChanged();
                    DisableUserCommand.RaiseCanExecuteChanged();
                    ResetPasswordCommand.RaiseCanExecuteChanged();
                    AddToGroupCommand.RaiseCanExecuteChanged();
                    RemoveFromGroupCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public AdGroup? SelectedMemberGroup
        {
            get => _selectedMemberGroup;
            set
            {
                if (SetField(ref _selectedMemberGroup, value))
                {
                    RemoveFromGroupCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public AdGroup? SelectedAvailableGroup
        {
            get => _selectedAvailableGroup;
            set
            {
                if (SetField(ref _selectedAvailableGroup, value))
                {
                    AddToGroupCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsNotBusy));
                    SearchCommand.RaiseCanExecuteChanged();
                    ListAllCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                    EnableUserCommand.RaiseCanExecuteChanged();
                    DisableUserCommand.RaiseCanExecuteChanged();
                    ResetPasswordCommand.RaiseCanExecuteChanged();
                    AddToGroupCommand.RaiseCanExecuteChanged();
                    RemoveFromGroupCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsNotBusy => !IsBusy;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand ListAllCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand EnableUserCommand { get; }
        public RelayCommand DisableUserCommand { get; }
        public RelayCommand ResetPasswordCommand { get; }
        public RelayCommand AddToGroupCommand { get; }
        public RelayCommand RemoveFromGroupCommand { get; }
        public RelayCommand ShowSettingsCommand { get; }

        private async Task SearchAsync()
        {
            Cancel();
            _cts = new CancellationTokenSource();
            IsBusy = true;
            StatusMessage = $"Suche nach '{SearchTerm}'...";
            try
            {
                var results = await _adService.SearchUsersAsync(SearchTerm, SearchType, _cts.Token);
                Users.Clear();
                foreach (var user in results.OrderBy(u => u.SamAccountName))
                {
                    Users.Add(user);
                }
                StatusMessage = $"{Users.Count} Benutzer gefunden.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Suche abgebrochen.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler bei der Suche: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler bei der Suche", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ListAllAsync()
        {
            Cancel();
            _cts = new CancellationTokenSource();
            IsBusy = true;
            StatusMessage = "Lade alle Benutzer...";
            try
            {
                var results = await _adService.GetAllUsersAsync(_cts.Token);
                Users.Clear();
                foreach (var user in results.OrderBy(u => u.SamAccountName))
                {
                    Users.Add(user);
                }
                StatusMessage = $"{Users.Count} Benutzer geladen.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Laden abgebrochen.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Laden: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Laden", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task EnableUserAsync()
        {
            if (SelectedUser == null) return;
            try
            {
                IsBusy = true;
                StatusMessage = $"Aktiviere '{SelectedUser.SamAccountName}'...";
                await _adService.EnableUserAsync(SelectedUser);
                SelectedUser.Enabled = true;
                EnableUserCommand.RaiseCanExecuteChanged();
                DisableUserCommand.RaiseCanExecuteChanged();
                StatusMessage = $"Benutzer '{SelectedUser.SamAccountName}' wurde aktiviert.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Aktivieren: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Aktivieren", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisableUserAsync()
        {
            if (SelectedUser == null) return;
            var confirm = MessageBox.Show(
                $"Möchten Sie den Benutzer '{SelectedUser.SamAccountName}' wirklich deaktivieren?",
                "Benutzer deaktivieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Deaktiviere '{SelectedUser.SamAccountName}'...";
                await _adService.DisableUserAsync(SelectedUser);
                SelectedUser.Enabled = false;
                EnableUserCommand.RaiseCanExecuteChanged();
                DisableUserCommand.RaiseCanExecuteChanged();
                StatusMessage = $"Benutzer '{SelectedUser.SamAccountName}' wurde deaktiviert.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Deaktivieren: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Deaktivieren", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ResetPasswordAsync()
        {
            if (SelectedUser == null) return;
            // Vereinfachter Dialog: Passwort wird hier fest auf "TempPass!123" gesetzt.
            // In einer produktiven Version sollte hier ein eigenes Passwort-Dialogfenster geöffnet werden.
            const string newPassword = "TempPass!123";
            var confirm = MessageBox.Show(
                $"Soll das Passwort für '{SelectedUser.SamAccountName}' auf ein temporäres Passwort zurückgesetzt werden?",
                "Passwort zurücksetzen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Setze Passwort für '{SelectedUser.SamAccountName}' zurück...";
                await _adService.ResetPasswordAsync(SelectedUser, newPassword);
                StatusMessage = $"Passwort für '{SelectedUser.SamAccountName}' wurde zurückgesetzt.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Zurücksetzen: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Passwort-Reset", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadUserGroupsAsync()
        {
            if (SelectedUser == null) return;
            try
            {
                IsBusy = true;
                StatusMessage = "Lade Gruppen...";

                var memberGroups = await _adService.GetUserGroupsAsync(SelectedUser);
                SelectedUser.MemberOf.Clear();
                foreach (var g in memberGroups.OrderBy(g => g.Name))
                {
                    SelectedUser.MemberOf.Add(g);
                }

                var allGroups = await _adService.GetAllGroupsAsync();
                var memberNames = new HashSet<string>(SelectedUser.MemberOf.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);
                AvailableGroups.Clear();
                foreach (var g in allGroups.Where(g => !memberNames.Contains(g.Name)).OrderBy(g => g.Name))
                {
                    AvailableGroups.Add(g);
                }

                StatusMessage = $"{SelectedUser.MemberOf.Count} Gruppen geladen, {AvailableGroups.Count} verfügbar.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Laden der Gruppen: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddToGroupAsync()
        {
            if (SelectedUser == null || SelectedAvailableGroup == null) return;
            try
            {
                IsBusy = true;
                var group = SelectedAvailableGroup;
                StatusMessage = $"Füge '{SelectedUser.SamAccountName}' zu '{group.Name}' hinzu...";
                await _adService.AddUserToGroupAsync(SelectedUser, group);
                SelectedUser.MemberOf.Add(group);
                AvailableGroups.Remove(group);
                StatusMessage = $"Benutzer wurde zu '{group.Name}' hinzugefügt.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Hinzufügen: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Hinzufügen zur Gruppe", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RemoveFromGroupAsync()
        {
            if (SelectedUser == null || SelectedMemberGroup == null) return;
            try
            {
                IsBusy = true;
                var group = SelectedMemberGroup;
                StatusMessage = $"Entferne '{SelectedUser.SamAccountName}' aus '{group.Name}'...";
                await _adService.RemoveUserFromGroupAsync(SelectedUser, group);
                SelectedUser.MemberOf.Remove(group);
                AvailableGroups.Add(group);
                StatusMessage = $"Benutzer wurde aus '{group.Name}' entfernt.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Entfernen: {ex.Message}";
                MessageBox.Show(ex.Message, "Fehler beim Entfernen aus Gruppe", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowSettings()
        {
            MessageBox.Show(
                "Verbindungseinstellungen würden hier konfiguriert.\n\n" +
                "In dieser Vorabversion wird die aktuelle Anmeldedomain verwendet. " +
                "Erweitern Sie den Konstruktor von MainViewModel, um Container, Benutzername und Passwort zu übergeben.",
                "Verbindungseinstellungen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
