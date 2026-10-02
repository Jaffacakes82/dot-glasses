using DotGlasses.Application.Common;
using DotGlasses.Application.CustomOrders;
using DotGlasses.Application.Customers;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Rules.LensPowers;

namespace DotGlasses.Application.Leads;

public class LeadService(
    ILeadRepository repository,
    IVisionTestRepository testRepository,
    ICustomerRepository customerRepository,
    ICustomOrderRepository customOrderRepository,
    IUnitOfWork unitOfWork) : ILeadService
{
    public async Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : (await ToDtosAsync([entity], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<LeadDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await repository.ListAsync(cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<LeadDto>> ListOpenAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await repository.ListOpenAsync(cancellationToken), cancellationToken);

    /// <summary>A Lead's DTO needs three things that aren't on its own row — the customer, the
    /// Coating set an ordering Lead carries, and the order it placed — each read once for the
    /// whole batch rather than once per Lead.</summary>
    private async Task<IReadOnlyList<LeadDto>> ToDtosAsync(IReadOnlyList<Lead> entities, CancellationToken cancellationToken)
    {
        var ids = entities.Select(l => l.Id).ToList();
        var customers = await customerRepository.GetByIdsAsync(entities.Select(l => l.CustomerId), cancellationToken);
        var coatings = await repository.GetCoatingRefIdsByLeadIdsAsync(ids, cancellationToken);
        var orders = await customOrderRepository.GetByLeadIdsAsync(ids, cancellationToken);

        return entities
            .Select(l => ToDto(l, customers.GetValueOrDefault(l.CustomerId), coatings.GetValueOrDefault(l.Id, []), orders.GetValueOrDefault(l.Id)))
            .ToList();
    }

    /// <summary>The most recent open Lead for an exact name+phone match — backs the Field App's
    /// "convert this instead?" prompt when recording a Sale for a customer who already has an
    /// unconverted Lead. Null if there's no Customer match at all, or the matching Customer has
    /// no open Lead.</summary>
    public async Task<LeadDto?> FindOpenMatchAsync(string hierarchyPath, string fullName, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
        {
            throw new DomainRuleViolationException("Your account isn't assigned to an organisation, so it can't look up a lead. Ask an admin to assign you.");
        }

        var customer = await customerRepository.FindByNameAndPhoneAsync(hierarchyPath, fullName, phoneNumber, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var entity = await repository.FindOpenByCustomerIdAsync(customer.Id, cancellationToken);
        return entity is null ? null : (await ToDtosAsync([entity], cancellationToken))[0];
    }

    public async Task<LeadDto> CreateAsync(CreateLeadRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
        {
            throw new DomainRuleViolationException("Your account isn't assigned to an organisation, so it can't record a lead. Ask an admin to assign you.");
        }

        // Already recorded (the outbox retrying): answered with what exists, so a repeat places no
        // second order.
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return (await ToDtosAsync([existing], cancellationToken))[0];
        }

        // Resolved before anything is built, so a refusal leaves nothing half-written — not even
        // a Customer row.
        var sourceTest = await ResolveSourceTestAsync(request.SourceTestId, cancellationToken);

        var customer = await FindOrCreateCustomerAsync(hierarchyPath, request.FullName, request.PhoneNumber, cancellationToken);

        // Stored the way a lens set's lens is (LensPowerRules.Normalise) — see VisionTestService.
        var left = LensPowerRules.Normalise(request.CylinderLeft, request.AxisLeft, request.AddLeft);
        var right = LensPowerRules.Normalise(request.CylinderRight, request.AxisRight, request.AddRight);

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
            ReferredOrTreated = request.ReferredOrTreated,
            ReferralReasonRefId = request.ReferralReasonRefId,
            ReferralOtherText = request.ReferralOtherText,
            ReferralLocationFreeText = request.ReferralLocationFreeText,
            TreatedInFacility = request.TreatedInFacility,
            ReasonNotPurchasedRefId = request.ReasonNotPurchasedRefId,
            ReasonNotPurchasedOtherText = request.ReasonNotPurchasedOtherText,
            CustomerToldPrice = request.CustomerToldPrice,
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

        // A Lead that orders its lens carries the Coating set it was ordered with, and places the
        // order in this same unit of work (ADR-0008): the Lead and its order exist together or
        // not at all.
        CustomOrder? order = null;
        var coatingRefIds = request.OrderFromDotGlasses ? request.CoatingRefIds.Distinct().ToList() : [];
        if (request.OrderFromDotGlasses)
        {
            repository.AddCoatings(coatingRefIds.Select(coatingRefId => new LeadCoating
            {
                Id = Guid.NewGuid(),
                LeadId = entity.Id,
                CoatingRefId = coatingRefId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }));

            order = new CustomOrder
            {
                Id = Guid.NewGuid(),
                HierarchyPath = hierarchyPath,
                Status = Domain.Enums.FulfilmentStatus.Submitted,
                PlacedAtUtc = DateTimeOffset.UtcNow,
                LeadId = entity.Id,
            };
            customOrderRepository.Add(order);
        }

        if (sourceTest is not null)
        {
            sourceTest.ConvertedToLeadId = entity.Id;
            testRepository.Update(sourceTest);
        }

        // Single SaveChangesAsync call: the Lead create, its order (if any) and the source Test's
        // ConvertedToLeadId update (if any) commit atomically in one transaction — see CLAUDE.md's
        // IUnitOfWork note.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity, customer, coatingRefIds, order);
    }

    /// <summary>
    /// The source Test a conversion names, or null when this Lead isn't a conversion at all.
    ///
    /// A named-but-unreadable source is a refusal, never a skipped back-link. The repository read
    /// goes through the global hierarchy filter, so "not found" covers both a Test that doesn't
    /// exist and one sitting outside the caller's own subtree — and until ticket 16 the miss was
    /// swallowed, leaving the Lead recorded, the Test still reading as unconverted, and the
    /// caller told the conversion had worked. Refusing keeps the pair all-or-nothing (ADR-0003).
    /// </summary>
    private async Task<Test?> ResolveSourceTestAsync(Guid? sourceTestId, CancellationToken cancellationToken)
    {
        if (sourceTestId is not { } id)
        {
            return null;
        }

        return await testRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainRuleViolationException(
                "The Test this Lead was converted from isn't available at your location — nothing has been saved.");
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

    /// <summary>Internal rather than private: SaleService builds the same DTO for the Lead a
    /// Sale converts, to ask OrderedLeadConversion the one question both layers ask.</summary>
    internal static LeadDto ToDto(Lead entity, Customer? customer, IReadOnlyList<Guid> coatingRefIds, CustomOrder? order) => new()
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
        ReferredOrTreated = entity.ReferredOrTreated,
        ReferralReasonRefId = entity.ReferralReasonRefId,
        ReferralOtherText = entity.ReferralOtherText,
        ReferralLocationFreeText = entity.ReferralLocationFreeText,
        TreatedInFacility = entity.TreatedInFacility,
        ReasonNotPurchasedRefId = entity.ReasonNotPurchasedRefId,
        ReasonNotPurchasedOtherText = entity.ReasonNotPurchasedOtherText,
        CustomerToldPrice = entity.CustomerToldPrice,
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
        OrderFromDotGlasses = order is not null,
        CoatingRefIds = coatingRefIds.ToList(),
        CustomOrderStatus = order?.Status.ToContract(),
        ConvertedFlag = entity.ConvertedFlag,
        SaleId = entity.SaleId,
        CreatedAtUtc = entity.CreatedAtUtc,
        ModifiedAtUtc = entity.ModifiedAtUtc,
    };
}
