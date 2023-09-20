using System.Threading;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Gameboard.Api.Services;
using Gameboard.Api.Structure.MediatR;
using Gameboard.Api.Structure.MediatR.Authorizers;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Gameboard.Api.Features.Sponsors;

public record CreateSponsorRequest(NewSponsor Model, User ActingUser) : IRequest<Sponsor>;

internal class CreateSponsorHandler : IRequestHandler<CreateSponsorRequest, Sponsor>
{
    private readonly SponsorService _sponsorService;
    private readonly IStore _store;
    private readonly UserRoleAuthorizer _userRoleAuthorizer;
    private readonly IValidatorService<CreateSponsorRequest> _validatorService;

    public CreateSponsorHandler
    (
        SponsorService sponsorService,
        IStore store,
        UserRoleAuthorizer userRoleAuthorizer,
        IValidatorService<CreateSponsorRequest> validatorService
    )
    {
        _sponsorService = sponsorService;
        _store = store;
        _userRoleAuthorizer = userRoleAuthorizer;
        _validatorService = validatorService;
    }

    public async Task<Sponsor> Handle(CreateSponsorRequest request, CancellationToken cancellationToken)
    {
        _userRoleAuthorizer.AllowedRoles = new UserRole[] { UserRole.Admin, UserRole.Registrar };
        _userRoleAuthorizer.Authorize();

        // create sponsor without logo - we can upload after
        var sponsor = await _store.Create(new Data.Sponsor { Name = request.Model.Name, Approved = true });

        // if they have a logo file, add that and clean up the old one
        var logoFileName = string.Empty;
        if (request.Model.LogoFile is not null)
            logoFileName = await _sponsorService.SetLogo(sponsor.Id, request.Model.LogoFile, cancellationToken);

        return new Sponsor
        {
            Id = sponsor.Id,
            Name = sponsor.Name,
            Logo = logoFileName
        };
    }
}
