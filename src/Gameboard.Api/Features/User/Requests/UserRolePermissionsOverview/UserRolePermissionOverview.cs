using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gameboard.Api.Common.Services;
using Gameboard.Api.Structure.MediatR;
using MediatR;

namespace Gameboard.Api.Features.Users;

public record UserRolePermissionsOverviewQuery() : IRequest<UserRolePermissionsOverviewResponse>;

internal sealed class UserRolePermissionsOverviewHandler(
    IActingUserService actingUserService,
    IUserRolePermissionsService permissionsService,
    IValidatorService validatorService
    ) : IRequestHandler<UserRolePermissionsOverviewQuery, UserRolePermissionsOverviewResponse>
{
    private readonly IActingUserService _actingUserService = actingUserService;
    private readonly IUserRolePermissionsService _permissionsService = permissionsService;
    private readonly IValidatorService _validatorService = validatorService;

    public async Task<UserRolePermissionsOverviewResponse> Handle(UserRolePermissionsOverviewQuery request, CancellationToken cancellationToken)
    {
        await _validatorService
            .ConfigureAuthorization(a => a.RequirePermissions(PermissionKey.Admin_View))
            .Validate(cancellationToken);

        var permissions = await _permissionsService.List();
        var groupedPermissions = permissions
            .GroupBy(p => p.Group)
            .ToDictionary(kv => kv.Key, kv => kv.ToList());

        return new()
        {
            Categories = groupedPermissions.Select(kv => new UserRolePermissionCategory
            {
                Name = kv.Key.ToString(),
                Permissions = kv.Value
            }),
            RolePermissions = await _permissionsService.GetAllRolePermissions(),
            YourRole = _permissionsService.ResolveSingle(_actingUserService.Get().Role)
        };
    }
}