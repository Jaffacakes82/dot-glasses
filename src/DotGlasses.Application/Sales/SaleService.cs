using DotGlasses.Application.Common;
using DotGlasses.Application.Customers;
using DotGlasses.Application.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Entities;
using DomainFrameCoverage = DotGlasses.Domain.Enums.FrameCoverage;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Application.Sales;

public class SaleService(
    ISaleRepository repository,
    ILeadRepository leadRepository,
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork) : ISaleService
{
    public async Task<SaleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var coatingsBySale = await repository.GetCoatingRefIdsBySaleIdsAsync([entity.Id], cancellationToken);
        return ToDto(entity, coatingsBySale.GetValueOrDefault(entity.Id, []));
    }

    public async Task<IReadOnlyList<SaleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAsync(cancellationToken);
        var coatingsBySale = await repository.GetCoatingRefIdsBySaleIdsAsync(entities.Select(e => e.Id).ToList(), cancellationToken);
        return entities.Select(e => ToDto(e, coatingsBySale.GetValueOrDefault(e.Id, []))).ToList();
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            var existingCoatings = await repository.GetCoatingRefIdsBySaleIdsAsync([existing.Id], cancellationToken);
            return ToDto(existing, existingCoatings.GetValueOrDefault(existing.Id, []));
        }

        var customerId = await FindOrCreateCustomerAsync(hierarchyPath, request.FullName, request.PhoneNumber, cancellationToken);
        var lensRangeType = request.LensRangeType.ToDomain();

        var entity = new Sale
        {
            Id = request.Id,
            HierarchyPath = hierarchyPath,
            TechnicianUserId = technicianUserId,
            CustomerId = customerId,
            SourceLeadId = request.SourceLeadId,
            AgeYears = request.AgeYears,
            Gender = request.Gender.ToDomain(),
            OccupationRefId = request.OccupationRefId,
            OccupationOtherText = request.OccupationOtherText,
            ConsentGiven = request.ConsentGiven,
            LensRangeType = lensRangeType,
            PresetCatalogueId = request.PresetCatalogueId,
            LensOptionLeftId = request.LensOptionLeftId,
            LensOptionRightId = request.LensOptionRightId,
            CustomSphereLeft = request.CustomSphereLeft,
            CustomCylinderLeft = request.CustomCylinderLeft,
            CustomAxisLeft = request.CustomAxisLeft,
            CustomAddPowerLeft = request.CustomAddPowerLeft,
            CustomSphereRight = request.CustomSphereRight,
            CustomCylinderRight = request.CustomCylinderRight,
            CustomAxisRight = request.CustomAxisRight,
            CustomAddPowerRight = request.CustomAddPowerRight,
            OrderFromDotGlasses = request.OrderFromDotGlasses,
            FulfilmentStatus = request.OrderFromDotGlasses ? Domain.Enums.FulfilmentStatus.Submitted : null,
            PupilDistanceMm = request.PupilDistanceMm,
            PresetPupilDistanceBucket = request.PresetPupilDistanceBucket,
            ChildrensFrame = request.ChildrensFrame,
            FrameColourRefId = request.FrameColourRefId,
            FrameColourOtherText = request.FrameColourOtherText,
            FrameCoverage = ToDomainFrameCoverage(request.FrameCoverage),
            HardCaseSold = request.HardCaseSold,
            HardCaseColourRefId = request.HardCaseColourRefId,
            HardCaseOtherColourText = request.HardCaseOtherColourText,
        };

        repository.Add(entity);
        repository.AddCoatings(request.CoatingRefIds.Distinct().Select(coatingRefId => new SaleCoating
        {
            Id = Guid.NewGuid(),
            SaleId = entity.Id,
            CoatingRefId = coatingRefId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }));

        if (request.SourceLeadId is { } sourceLeadId)
        {
            var sourceLead = await leadRepository.GetByIdAsync(sourceLeadId, cancellationToken);
            if (sourceLead is not null)
            {
                sourceLead.ConvertedFlag = true;
                sourceLead.SaleId = entity.Id;
                leadRepository.Update(sourceLead);
            }
        }

        // Single SaveChangesAsync call: the Sale create and the source Lead's ConvertedFlag/
        // SaleId update (if any) commit atomically — see CLAUDE.md's IUnitOfWork note.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity, request.CoatingRefIds);
    }

    /// <summary>Exact name+phone match within the retail point — see LeadService's identical helper.</summary>
    private async Task<Guid> FindOrCreateCustomerAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken)
    {
        var existing = await customerRepository.FindByNameAndPhoneAsync(hierarchyPath, fullName, phoneNumber, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            FullName = fullName,
            PhoneNumber = phoneNumber,
        };

        customerRepository.Add(customer);
        return customer.Id;
    }

    private static SaleDto ToDto(Sale entity, IReadOnlyList<Guid> coatingRefIds) => new()
    {
        Id = entity.Id,
        HierarchyPath = entity.HierarchyPath,
        TechnicianUserId = entity.TechnicianUserId,
        CustomerId = entity.CustomerId,
        SourceLeadId = entity.SourceLeadId,
        AgeYears = entity.AgeYears,
        Gender = entity.Gender.ToContract(),
        OccupationRefId = entity.OccupationRefId,
        OccupationOtherText = entity.OccupationOtherText,
        ConsentGiven = entity.ConsentGiven,
        LensRangeType = entity.LensRangeType.ToContract(),
        PresetCatalogueId = entity.PresetCatalogueId,
        LensOptionLeftId = entity.LensOptionLeftId,
        LensOptionRightId = entity.LensOptionRightId,
        CustomSphereLeft = entity.CustomSphereLeft,
        CustomCylinderLeft = entity.CustomCylinderLeft,
        CustomAxisLeft = entity.CustomAxisLeft,
        CustomAddPowerLeft = entity.CustomAddPowerLeft,
        CustomSphereRight = entity.CustomSphereRight,
        CustomCylinderRight = entity.CustomCylinderRight,
        CustomAxisRight = entity.CustomAxisRight,
        CustomAddPowerRight = entity.CustomAddPowerRight,
        OrderFromDotGlasses = entity.OrderFromDotGlasses,
        PupilDistanceMm = entity.PupilDistanceMm,
        PresetPupilDistanceBucket = entity.PresetPupilDistanceBucket,
        ChildrensFrame = entity.ChildrensFrame,
        FrameColourRefId = entity.FrameColourRefId,
        FrameColourOtherText = entity.FrameColourOtherText,
        FrameCoverage = ToContractFrameCoverage(entity.FrameCoverage),
        CoatingRefIds = coatingRefIds.ToList(),
        HardCaseSold = entity.HardCaseSold,
        HardCaseColourRefId = entity.HardCaseColourRefId,
        HardCaseOtherColourText = entity.HardCaseOtherColourText,
        CreatedAtUtc = entity.CreatedAtUtc,
        ModifiedAtUtc = entity.ModifiedAtUtc,
    };

    private static DomainFrameCoverage ToDomainFrameCoverage(ContractFrameCoverage coverage) => coverage switch
    {
        ContractFrameCoverage.FullFrame => DomainFrameCoverage.FullFrame,
        ContractFrameCoverage.EyeFrameRimsOnly => DomainFrameCoverage.EyeFrameRimsOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(coverage), coverage, null),
    };

    private static ContractFrameCoverage ToContractFrameCoverage(DomainFrameCoverage coverage) => coverage switch
    {
        DomainFrameCoverage.FullFrame => ContractFrameCoverage.FullFrame,
        DomainFrameCoverage.EyeFrameRimsOnly => ContractFrameCoverage.EyeFrameRimsOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(coverage), coverage, null),
    };
}
