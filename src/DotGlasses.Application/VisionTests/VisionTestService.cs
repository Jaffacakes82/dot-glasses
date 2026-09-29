using DotGlasses.Application.Common;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Rules.LensPowers;
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
        if (string.IsNullOrEmpty(hierarchyPath))
        {
            throw new DomainRuleViolationException("Your account has no org assignment and cannot record a test.");
        }

        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        // Stored the way a lens set's lens is (LensPowerRules.Normalise), so a Custom +3.00 and a
        // lens set's +3.00 are the same row shape whichever way the client spelled "none".
        var left = LensPowerRules.Normalise(request.CylinderLeft, request.AxisLeft, request.AddLeft);
        var right = LensPowerRules.Normalise(request.CylinderRight, request.AxisRight, request.AddRight);

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
            LensRangeType = request.LensRangeType?.ToDomain(),
            PresetCatalogueId = request.PresetCatalogueId,
            SphereLeft = request.SphereLeft,
            CylinderLeft = left.Cylinder,
            AxisLeft = left.Axis,
            AddLeft = left.Add,
            SphereRight = request.SphereRight,
            CylinderRight = right.Cylinder,
            AxisRight = right.Axis,
            AddRight = right.Add,
            LensTypeRefId = request.LensTypeRefId,
            LensTypeOtherText = request.LensTypeOtherText,
            PupilDistanceMm = request.PupilDistanceMm,
            PresetPupilDistanceBucket = request.PresetPupilDistanceBucket,
            ChildrensFrame = request.ChildrensFrame,
            CoatingPreferenceRefId = request.CoatingPreferenceRefId,
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
        LensRangeType = entity.LensRangeType?.ToContract(),
        PresetCatalogueId = entity.PresetCatalogueId,
        SphereLeft = entity.SphereLeft,
        CylinderLeft = entity.CylinderLeft,
        AxisLeft = entity.AxisLeft,
        AddLeft = entity.AddLeft,
        SphereRight = entity.SphereRight,
        CylinderRight = entity.CylinderRight,
        AxisRight = entity.AxisRight,
        AddRight = entity.AddRight,
        LensTypeRefId = entity.LensTypeRefId,
        LensTypeOtherText = entity.LensTypeOtherText,
        PupilDistanceMm = entity.PupilDistanceMm,
        PresetPupilDistanceBucket = entity.PresetPupilDistanceBucket,
        ChildrensFrame = entity.ChildrensFrame,
        CoatingPreferenceRefId = entity.CoatingPreferenceRefId,
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
