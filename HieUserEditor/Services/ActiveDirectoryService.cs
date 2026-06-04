using HieUserEditor.Models;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HieUserEditor.Services
{
    /// <summary>
    /// Implementierung von <see cref="IActiveDirectoryService"/> auf Basis von
    /// <see cref="System.DirectoryServices.AccountManagement"/>.
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly string _domain;
        private readonly string? _container;
        private readonly string? _username;
        private readonly string? _password;

        public ActiveDirectoryService(
            string domain,
            string? container = null,
            string? username = null,
            string? password = null)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _container = container;
            _username = username;
            _password = password;
        }

        private PrincipalContext CreateContext()
        {
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                return new PrincipalContext(
                    ContextType.Domain,
                    _domain,
                    _container,
                    ContextOptions.Negotiate,
                    _username,
                    _password);
            }
            return new PrincipalContext(ContextType.Domain, _domain, _container);
        }

        public Task<IEnumerable<AdUser>> SearchUsersAsync(
            string term,
            SearchType type,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var results = new List<AdUser>();
                using var context = CreateContext();
                using var filter = new UserPrincipal(context);

                switch (type)
                {
                    case SearchType.Name:
                    case SearchType.All:
                        filter.Name = $"*{term}*";
                        break;
                    case SearchType.Email:
                        filter.EmailAddress = $"*{term}*";
                        break;
                }

                using var enumerator = UserPrincipal.FindAll(filter).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (enumerator.Current is UserPrincipal principal)
                    {
                        results.Add(MapToAdUser(principal));
                    }
                }

                return (IEnumerable<AdUser>)results;
            }, cancellationToken);
        }

        public Task<IEnumerable<AdUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var results = new List<AdUser>();
                using var context = CreateContext();
                using var filter = new UserPrincipal(context);
                using var enumerator = UserPrincipal.FindAll(filter).GetEnumerator();

                while (enumerator.MoveNext())
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (enumerator.Current is UserPrincipal principal)
                    {
                        results.Add(MapToAdUser(principal));
                    }
                }

                return (IEnumerable<AdUser>)results;
            }, cancellationToken);
        }

        public Task EnableUserAsync(AdUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return Task.Run(() =>
            {
                using var context = CreateContext();
                using var principal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                if (principal == null)
                    throw new InvalidOperationException($"Benutzer '{user.SamAccountName}' nicht gefunden.");
                principal.Enabled = true;
                principal.Save();
            });
        }

        public Task DisableUserAsync(AdUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return Task.Run(() =>
            {
                using var context = CreateContext();
                using var principal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                if (principal == null)
                    throw new InvalidOperationException($"Benutzer '{user.SamAccountName}' nicht gefunden.");
                principal.Enabled = false;
                principal.Save();
            });
        }

        public Task ResetPasswordAsync(AdUser user, string newPassword)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("Passwort darf nicht leer sein.", nameof(newPassword));

            return Task.Run(() =>
            {
                using var context = CreateContext();
                using var principal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                if (principal == null)
                    throw new InvalidOperationException($"Benutzer '{user.SamAccountName}' nicht gefunden.");
                principal.SetPassword(newPassword);
                principal.Save();
            });
        }

        public Task<IEnumerable<AdGroup>> GetUserGroupsAsync(AdUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return Task.Run(() =>
            {
                var results = new List<AdGroup>();
                using var context = CreateContext();
                using var principal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                if (principal == null) return (IEnumerable<AdGroup>)results;

                foreach (var group in principal.GetGroups())
                {
                    if (group is GroupPrincipal gp)
                    {
                        results.Add(new AdGroup
                        {
                            Name = gp.Name ?? string.Empty,
                            DistinguishedName = gp.DistinguishedName ?? string.Empty
                        });
                    }
                }

                return (IEnumerable<AdGroup>)results;
            });
        }

        public Task<IEnumerable<AdGroup>> GetAllGroupsAsync()
        {
            return Task.Run(() =>
            {
                var results = new List<AdGroup>();
                using var context = CreateContext();
                using var filter = new GroupPrincipal(context);

                foreach (var group in GroupPrincipal.FindAll(filter))
                {
                    results.Add(new AdGroup
                    {
                        Name = group.Name ?? string.Empty,
                        DistinguishedName = group.DistinguishedName ?? string.Empty,
                        Description = group.Description ?? string.Empty
                    });
                }
                return (IEnumerable<AdGroup>)results;
            });
        }

        public Task AddUserToGroupAsync(AdUser user, AdGroup group)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (group == null) throw new ArgumentNullException(nameof(group));

            return Task.Run(() =>
            {
                using var context = CreateContext();
                using var userPrincipal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                using var groupPrincipal = GroupPrincipal.FindByIdentity(
                    context, IdentityType.Name, group.Name);

                if (userPrincipal == null)
                    throw new InvalidOperationException($"Benutzer '{user.SamAccountName}' nicht gefunden.");
                if (groupPrincipal == null)
                    throw new InvalidOperationException($"Gruppe '{group.Name}' nicht gefunden.");

                groupPrincipal.Members.Add(userPrincipal);
                groupPrincipal.Save();
            });
        }

        public Task RemoveUserFromGroupAsync(AdUser user, AdGroup group)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (group == null) throw new ArgumentNullException(nameof(group));

            return Task.Run(() =>
            {
                using var context = CreateContext();
                using var userPrincipal = UserPrincipal.FindByIdentity(
                    context, IdentityType.SamAccountName, user.SamAccountName);
                using var groupPrincipal = GroupPrincipal.FindByIdentity(
                    context, IdentityType.Name, group.Name);

                if (userPrincipal == null)
                    throw new InvalidOperationException($"Benutzer '{user.SamAccountName}' nicht gefunden.");
                if (groupPrincipal == null)
                    throw new InvalidOperationException($"Gruppe '{group.Name}' nicht gefunden.");

                groupPrincipal.Members.Remove(userPrincipal);
                groupPrincipal.Save();
            });
        }

        private static AdUser MapToAdUser(UserPrincipal principal)
        {
            var ou = string.Empty;
            if (!string.IsNullOrEmpty(principal.DistinguishedName))
            {
                var firstOu = principal.DistinguishedName
                    .Split(',')
                    .FirstOrDefault(p => p.Trim().StartsWith("OU=", StringComparison.OrdinalIgnoreCase));
                if (firstOu != null) ou = firstOu.Trim().Substring(3);
            }

            return new AdUser
            {
                SamAccountName = principal.SamAccountName ?? string.Empty,
                DisplayName = principal.DisplayName ?? string.Empty,
                Email = principal.EmailAddress ?? string.Empty,
                Enabled = principal.Enabled ?? false,
                DistinguishedName = principal.DistinguishedName ?? string.Empty,
                Ou = ou
            };
        }
    }
}
