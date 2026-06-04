using HieUserEditor.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HieUserEditor.Services
{
    /// <summary>
    /// Suchtyp für Benutzerabfragen.
    /// </summary>
    public enum SearchType
    {
        Name,
        Email,
        All
    }

    /// <summary>
    /// Schnittstelle für den Active Directory Zugriff.
    /// </summary>
    public interface IActiveDirectoryService
    {
        Task<IEnumerable<AdUser>> SearchUsersAsync(string term, SearchType type, CancellationToken cancellationToken = default);
        Task<IEnumerable<AdUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        Task EnableUserAsync(AdUser user);
        Task DisableUserAsync(AdUser user);
        Task ResetPasswordAsync(AdUser user, string newPassword);
        Task<IEnumerable<AdGroup>> GetUserGroupsAsync(AdUser user);
        Task<IEnumerable<AdGroup>> GetAllGroupsAsync();
        Task AddUserToGroupAsync(AdUser user, AdGroup group);
        Task RemoveUserFromGroupAsync(AdUser user, AdGroup group);
    }
}
