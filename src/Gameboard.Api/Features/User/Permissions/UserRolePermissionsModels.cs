using System.Collections.Generic;
using Gameboard.Api.Data;
using Gameboard.Api.Structure;

namespace Gameboard.Api.Features.Users;

public enum PermissionKey
{
    Admin_CreateApiKeys,
    Admin_CreateEditSponsors,
    Admin_View,
    Games_AdminExternal,
    Games_CreateEditDelete,
    Games_ViewUnpublished,
    Play_ChooseChallengeVariant,
    Play_IgnoreSessionResetSettings,
    Play_IgnoreExecutionWindow,
    Practice_EditSettings,
    Reports_View,
    Scores_AwardManualBonuses,
    Scores_RegradeAndRerank,
    Scores_ViewLive,
    Support_EditSettings,
    Support_ManageTickets,
    Support_ViewTickets,
    SystemNotifications_CreateEdit,
    Teams_ApproveNameChanges,
    Teams_DeployGameResources,
    Teams_EditSession,
    Teams_Enroll,
    Teams_Observe,
    Teams_SendAnnouncements,
    Users_CreateEditDelete,
    Users_EditRoles
}

public enum PermissionKeyGroup
{
    Admin,
    Games,
    Play,
    Practice,
    Reports,
    Scoring,
    Support,
    Teams,
    Users
}

public sealed class UserRolePermissionCategory
{
    public required string Name { get; set; }
    public required IEnumerable<UserRolePermission> Permissions { get; set; }
}

public sealed class UserRolePermission
{
    public required PermissionKeyGroup Group { get; set; }
    public required PermissionKey Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}

public sealed class UserRolePermissionException : GameboardValidationException
{
    public UserRolePermissionException(UserRole role, IEnumerable<PermissionKey> requiredPermissions)
        : base($"This operation requires the following permission(s): {string.Join(",", requiredPermissions)}. Your role ({role}) does not have one or more of these.") { }
}
