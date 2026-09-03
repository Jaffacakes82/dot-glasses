using DotGlasses.Application.Common;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Entities;
using DomainOutcome = DotGlasses.Domain.Enums.TestOutcome;
using ContractOutcome = DotGlasses.Contracts.Tests.TestOutcome;

namespace DotGlasses.Application.VisionTests;

public class VisionTestService(IVisionTestRepository repository, IUnitOfWork unitOfWork) : IVisionTestService
{
    public async Task<TestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<TestDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAsync(cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<TestDto> CreateAsync(CreateTestRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var entity = new Test
        {
            Id = request.Id,
            HierarchyPath = hierarchyPath,
            TechnicianUserId = technicianUserId,
            AgeYears = request.AgeYears,
            Gender = request.Gender.ToDomain(),
            OccupationRefId = request.OccupationRefId,
            OccupationOtherText = request.OccupationOtherText,
            Outcome = ToDomainOutcome(request.Outcome),
            ReferredOrTreated = request.ReferredOrTreated,
            ReferralReasonRefId = request.ReferralReasonRefId,
            ReferralOtherText = request.ReferralOtherText,
            ReferralLocationFreeText = request.ReferralLocationFreeText,
            TreatedInFacility = request.TreatedInFacility,
        };

        repository.Add(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    private static TestDto ToDto(Test entity) => new()
    {
        Id = entity.Id,
        HierarchyPath = entity.HierarchyPath,
        TechnicianUserId = entity.TechnicianUserId,
        AgeYears = entity.AgeYears,
        Gender = entity.Gender.ToContract(),
        OccupationRefId = entity.OccupationRefId,
        OccupationOtherText = entity.OccupationOtherText,
        Outcome = ToContractOutcome(entity.Outcome),
        ReferredOrTreated = entity.ReferredOrTreated,
        ReferralReasonRefId = entity.ReferralReasonRefId,
        ReferralOtherText = entity.ReferralOtherText,
        ReferralLocationFreeText = entity.ReferralLocationFreeText,
        TreatedInFacility = entity.TreatedInFacility,
        ConvertedToLeadId = entity.ConvertedToLeadId,
        CreatedAtUtc = entity.CreatedAtUtc,
        ModifiedAtUtc = entity.ModifiedAtUtc,
    };

    private static DomainOutcome ToDomainOutcome(ContractOutcome outcome) => outcome switch
    {
        ContractOutcome.NoGlassesNeeded => DomainOutcome.NoGlassesNeeded,
        ContractOutcome.NeedsGlasses => DomainOutcome.NeedsGlasses,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    private static ContractOutcome ToContractOutcome(DomainOutcome outcome) => outcome switch
    {
        DomainOutcome.NoGlassesNeeded => ContractOutcome.NoGlassesNeeded,
        DomainOutcome.NeedsGlasses => ContractOutcome.NeedsGlasses,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
