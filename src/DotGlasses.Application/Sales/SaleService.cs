using DotGlasses.Application.Common;
using DotGlasses.Application.CustomOrders;
using DotGlasses.Application.Customers;
using DotGlasses.Application.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.Sales;
using DomainFrameCoverage = DotGlasses.Domain.Enums.FrameCoverage;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Application.Sales;

public class SaleService(
    ISaleRepository repository,
    ILeadRepository leadRepository,
    ICustomerRepository customerRepository,
    ICustomOrderRepository customOrderRepository,
    IUnitOfWork unitOfWork) : ISaleService
{
    public async Task<SaleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : (await ToDtosAsync([entity], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<SaleDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await repository.ListAsync(cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<SaleDto>> ToDtosAsync(IReadOnlyList<Sale> entities, CancellationToken cancellationToken)
    {
        var ids = entities.Select(e => e.Id).ToList();
        var coatings = await repository.GetCoatingRefIdsBySaleIdsAsync(ids, cancellationToken);
        var orders = await customOrderRepository.GetBySaleIdsAsync(ids, cancellationToken);
        return entities.Select(e => ToDto(e, coatings.GetValueOrDefault(e.Id, []), orders.GetValueOrDefault(e.Id))).ToList();
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hierarchyPath))
        {
            throw new DomainRuleViolationException("Your account isn't assigned to an organisation, so it can't record a sale. Ask an admin to assign you.");
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
        var sourceLead = await ResolveSourceLeadAsync(request.SourceLeadId, cancellationToken);
        var leadsOrder = await ResolveLeadsOrderAsync(request, sourceLead, cancellationToken);

        var customerId = await FindOrCreateCustomerAsync(hierarchyPath, request.FullName, request.PhoneNumber, cancellationToken);
        var lensRangeType = request.LensRangeType.ToDomain();

        // Stored the way a lens set's lens is (LensPowerRules.Normalise) — see VisionTestService.
        var left = LensPowerRules.Normalise(request.CylinderLeft, request.AxisLeft, request.AddLeft);
        var right = LensPowerRules.Normalise(request.CylinderRight, request.AxisRight, request.AddRight);

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
            ReferredOrTreated = request.ReferredOrTreated,
            ReferralReasonRefId = request.ReferralReasonRefId,
            ReferralOtherText = request.ReferralOtherText,
            ReferralLocationFreeText = request.ReferralLocationFreeText,
            TreatedInFacility = request.TreatedInFacility,
            LensRangeType = lensRangeType,
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

        if (sourceLead is not null)
        {
            sourceLead.ConvertedFlag = true;
            sourceLead.SaleId = entity.Id;
            leadRepository.Update(sourceLead);
        }

        // The order behind this Sale (ADR-0008): the one its Lead already placed, now paid for —
        // linked, never duplicated — or a new one this Sale places. Either way it is part of the
        // same unit of work as the Sale.
        var order = leadsOrder;
        if (order is not null)
        {
            order.SaleId = entity.Id;
            customOrderRepository.Update(order);
        }
        else if (request.OrderFromDotGlasses)
        {
            order = CustomOrder.Place(hierarchyPath, saleId: entity.Id);
            customOrderRepository.Add(order);
        }

        // Single SaveChangesAsync call: the Sale create, its order, and the source Lead's
        // ConvertedFlag/SaleId update (if any) commit atomically — see CLAUDE.md's IUnitOfWork note.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(entity, request.CoatingRefIds, order);
    }

    /// <summary>
    /// The order the converted Lead already placed, or null when there is none. A Sale converting
    /// an ordered Lead must keep the lens and Coating set that were ordered and must not ask for
    /// another order — OrderedLeadConversion, the rule both controllers report field by field.
    /// This is the guard behind them: reached only by a caller that skipped that check, so it
    /// refuses the whole record in one sentence (ADR-0003).
    /// </summary>
    private async Task<CustomOrder?> ResolveLeadsOrderAsync(CreateSaleRequest request, Lead? sourceLead, CancellationToken cancellationToken)
    {
        if (sourceLead is null
            || !(await customOrderRepository.GetByLeadIdsAsync([sourceLead.Id], cancellationToken)).TryGetValue(sourceLead.Id, out var order))
        {
            return null;
        }

        var leadCoatings = await leadRepository.GetCoatingRefIdsByLeadIdsAsync([sourceLead.Id], cancellationToken);
        var lead = LeadService.ToDto(sourceLead, customer: null, leadCoatings.GetValueOrDefault(sourceLead.Id, []), order);
        if (!OrderedLeadConversion.Check(request, lead).IsValid)
        {
            throw new DomainRuleViolationException(
                "This lead's lens is already ordered, so the sale has to keep the lens and coatings that were ordered — nothing has been saved. To sell a different lens, record a new sale.");
        }

        return order;
    }

    /// <summary>
    /// The source Lead a conversion names, or null when this Sale isn't a conversion at all.
    /// Mirrors LeadService.ResolveSourceTestAsync — a named-but-unreadable source is a refusal,
    /// never a skipped back-link that would leave the Lead sitting in the open worklist while
    /// the Sale reads as recorded.
    /// </summary>
    private async Task<Lead?> ResolveSourceLeadAsync(Guid? sourceLeadId, CancellationToken cancellationToken)
    {
        if (sourceLeadId is not { } id)
        {
            return null;
        }

        var lead = await leadRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainRuleViolationException(
                "The Lead this Sale was converted from isn't available at your location — nothing has been saved.");

        // A Lead converts once. The controllers report this against the field first; this is the
        // guard behind them, and what stops a second Sale taking over the Lead's back-link — and,
        // for a Lead that ordered its lens, its order — from the Sale that already has them. (A
        // retry of that same Sale never gets here: CreateAsync has already answered it.)
        if (lead.SaleId is not null)
        {
            throw new DomainRuleViolationException("This lead has already been converted into a sale — nothing has been saved.");
        }

        return lead;
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

    private static SaleDto ToDto(Sale entity, IReadOnlyList<Guid> coatingRefIds, CustomOrder? order) => new()
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
        ReferredOrTreated = entity.ReferredOrTreated,
        ReferralReasonRefId = entity.ReferralReasonRefId,
        ReferralOtherText = entity.ReferralOtherText,
        ReferralLocationFreeText = entity.ReferralLocationFreeText,
        TreatedInFacility = entity.TreatedInFacility,
        LensRangeType = entity.LensRangeType.ToContract(),
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
        OrderFromDotGlasses = order is not null,
        CustomOrderStatus = order?.Status.ToContract(),
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
