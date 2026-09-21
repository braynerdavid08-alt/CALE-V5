using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryTrainingService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly ICatalogStore _catalog;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;
    private readonly ITrainingEligibilityService _eligibility;
    private readonly ISchoolMembershipGuard _membership;
    private readonly IConfiguration _config;
    private readonly ILogger<TheoryTrainingService> _logger;

    public TheoryTrainingService(
        CaleDbContext db,
        IUserStore users,
        ICatalogStore catalog,
        IClock clock,
        INotificationPublisher notifications,
        ITrainingEligibilityService eligibility,
        ISchoolMembershipGuard membership,
        IConfiguration config,
        ILogger<TheoryTrainingService> logger)
    {
        _db = db;
        _users = users;
        _catalog = catalog;
        _clock = clock;
        _notifications = notifications;
        _eligibility = eligibility;
        _membership = membership;
        _config = config;
        _logger = logger;
    }

    private bool AllowRequestPathRepair =>
        _config.GetValue("Database:AllowRequestPathRepair", false);
}
