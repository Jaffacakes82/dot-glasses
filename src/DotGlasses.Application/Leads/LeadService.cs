using DotGlasses.Application.Common;
using DotGlasses.Application.Customers;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Leads;

public class LeadService(
    ILeadRepository repository,
    IVisionTestRepository testRepository,
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork) : ILeadService
{
    public async Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var customer = await customerRepository.GetByIdAsync(entity.CustomerId, cancellationToken);
        return ToDto(entity, customer);
    }

    public async Task<IReadOnlyList<LeadDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAsync(cancellationToken);
        var customers = await customerRepository.GetByIdsAsync(entities.Select(l => l.CustomerId), cancellationToken);
        return entities.Select(l => ToDto(l, customers.GetValueOrDefault(l.CustomerId))).ToList();
    }

    public async Task<IReadOnlyList<LeadDto>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListOpenAsync(cancellationToken);
        var customers = await customerRepository.GetByIdsAsync(entities.Select(l => l.CustomerId), cancellationToken);
        return entities.Select(l => ToDto(l, customers.GetValueOrDefault(l.CustomerId))).ToList();
    }

    /// <summary>The most recent open Lead for an exact name+phone match — backs the Field App's
    /// "convert this instead?" prompt when recording a Sale for a customer who already has an
    /// unconverted Lead. Null if there's no Customer match at all, or the matching Customer has
    /// no open Lead.</summary>
    public async Task<LeadDto?> FindOpenMatchAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.FindByNameAndPhoneAsync(hierarchyPath, fullName, phoneNumber, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var entity = await repository.FindOpenByCustomerIdAsync(customer.Id, cancellationToken);
        return entity is null ? null : ToDto(entity, customer);
    }

    public async Task<LeadDto> CreateAsync(CreateLeadRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            var existingCustomer = await customerRepository.GetByIdAsync(existing.CustomerId, cancellationToken);
            return ToDto(existing, existingCustomer);
        }

        var customer = await FindOrCreateCustomerAsync(hierarchyPath, request.FullName, request.PhoneNumber, cancellationToken);

        var entity = new Lead
        {
            Id = request.Id,
            HierarchyPath = hierarchyPath,
            TechnicianUserId = technicianUserId,
            CustomerId = customer.Id,
            SourceTestId = request.SourceTestId,
            AgeYears = request.AgeYears,
            Gender = request.Gender.ToDomain(),
            OccupationRefId = request.OccupationRefId,
            OccupationOtherText = request.OccupationOtherText,
            ConsentGiven = request.ConsentGiven,
            ReasonNotPurchasedRefId = request.ReasonNotPurchasedRefId,
            ReasonNotPurchasedOtherText = request.ReasonNotPurchasedOtherText,
            LensRangeType = request.LensRangeType?.ToDomain(),
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
            LensTypeRefId = request.LensTypeRefId,
            LensTypeOtherText = request.LensTypeOtherText,
            PupilDistanceMm = request.PupilDistanceMm,
            PresetPupilDistanceBucket = request.PresetPupilDistanceBucket,
            ChildrensFrame = request.ChildrensFrame,
            CoatingPreferenceRefId = request.CoatingPreferenceRefId,
        };

        repository.Add(entity);

        if (request.SourceTestId is { } sourceTestId)
        {
            var sourceTest = await testRepository.GetByIdAsync(sourceTestId, cancellationToken);
            if (sourceTest is not null)
            {
                sourceTest.ConvertedToLeadId = entity.Id;
                testRepository.Update(sourceTest);
            }
        }

        // Single SaveChangesAsync call: the Lead create and the source Test's ConvertedToLeadId
        // update (if any) commit atomically in one transaction — see CLAUDE.md's IUnitOfWork note.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity, customer);
    }

    /// <summary>Exact name+phone match within the retail point — "don't create a duplicate
    /// Customer row for a repeat name+phone". Fuzzy/suggested-match UX is Field App UI work for
    /// later.</summary>
    private async Task<Customer> FindOrCreateCustomerAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken)
    {
        var existing = await customerRepository.FindByNameAndPhoneAsync(hierarchyPath, fullName, phoneNumber, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            FullName = fullName,
            PhoneNumber = phoneNumber,
        };

        customerRepository.Add(customer);
        return customer;
    }

    private static LeadDto ToDto(Lead entity, Customer? customer) => new()
    {
        Id = entity.Id,
        HierarchyPath = entity.HierarchyPath,
        TechnicianUserId = entity.TechnicianUserId,
        CustomerId = entity.CustomerId,
        CustomerFullName = customer?.FullName ?? "—",
        CustomerPhoneNumber = customer?.PhoneNumber,
        SourceTestId = entity.SourceTestId,
        AgeYears = entity.AgeYears,
        Gender = entity.Gender.ToContract(),
        OccupationRefId = entity.OccupationRefId,
        OccupationOtherText = entity.OccupationOtherText,
        ConsentGiven = entity.ConsentGiven,
        ReasonNotPurchasedRefId = entity.ReasonNotPurchasedRefId,
        ReasonNotPurchasedOtherText = entity.ReasonNotPurchasedOtherText,
        LensRangeType = entity.LensRangeType?.ToContract(),
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
        LensTypeRefId = entity.LensTypeRefId,
        LensTypeOtherText = entity.LensTypeOtherText,
        PupilDistanceMm = entity.PupilDistanceMm,
        PresetPupilDistanceBucket = entity.PresetPupilDistanceBucket,
        ChildrensFrame = entity.ChildrensFrame,
        CoatingPreferenceRefId = entity.CoatingPreferenceRefId,
        ConvertedFlag = entity.ConvertedFlag,
        SaleId = entity.SaleId,
        CreatedAtUtc = entity.CreatedAtUtc,
        ModifiedAtUtc = entity.ModifiedAtUtc,
    };
}
