using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules;

/// <summary>
/// The one entry point per consultation type, per ADR-0002. Rules are composed internally from
/// per-topic functions (referral, lens range, coating set, frame, hard case, occupation) but
/// exposed request-DTO-shaped, because <see cref="RuleFailure.Key"/> is a request-DTO property
/// name and both callers — the Field App's pre-submit check and the server's create endpoint —
/// hold the request, not the topics.
///
/// <b>Empty on purpose.</b> This is the expand phase: the surface exists so the migration batches
/// have somewhere to land, but not one rule has moved yet. Every consultation rule still lives in
/// DotGlasses.Web.Validation's three FluentValidation validators and in ConsultationForm.razor,
/// and those remain the only enforcement until they are deleted. Nothing calls this today —
/// <b>do not wire a controller or a form to it before those rules are actually here</b>, because
/// until then it reports every request as valid.
///
/// <b>Two consultation rules can never live here</b>, and the synchronous snapshot-only signature
/// is what keeps that honest: CreateLeadRequestValidator.ValidateSourceTestAsync and
/// CreateSaleRequestValidator.ValidateSourceLeadAsync check a specific Test/Lead row through
/// IVisionTestRepository/ILeadRepository. That is I/O against hierarchy-scoped data, not a fact
/// about the reference-data library, so it stays on the server in the controller or service layer.
/// Don't widen this surface to take a repository or return a Task to accommodate them.
/// </summary>
public static class ConsultationRules
{
    public static RuleResult Check(CreateTestRequest request, ReferenceDataSnapshot snapshot) => RuleResult.Valid;

    public static RuleResult Check(CreateLeadRequest request, ReferenceDataSnapshot snapshot) => RuleResult.Valid;

    public static RuleResult Check(CreateSaleRequest request, ReferenceDataSnapshot snapshot) => RuleResult.Valid;
}
