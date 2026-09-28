# Research: e-commerce custom lens option ranges vs the Field App

Ticket: `issues/05-custom-lens-option-ranges.md` (CEO feedback, 2026-09-27). Researched 2026-09-28.

**Question.** What does the Dot Glasses e-commerce site's prescription configurator offer, so the
Field App's Custom prescription form can copy it like for like? How does the Field App differ today?

## Sources

- **The configurator:** <https://dotglasses.org/dot-glasses-ecommerce/product.php?id=72>
  ("Eyeglasses", from KSH 850.00). It is the shop's only eyeglasses product. The other three
  products (id 73 Hard Case, 74 Arclight, 75 Arclight Holo) have no lens options.
- Shop index: <https://dotglasses.org/dot-glasses-ecommerce/index.php>
- Shop FAQ: <https://dotglasses.org/dot-glasses-ecommerce/faq.php>. It says only that PD, SPH, CYL
  and Axis are needed. It gives no ranges.
- <https://shop.dotglasses.org/> (e.g. `/Kenya`) is a **different** shop: the B2B/distributor kit
  shop. It sells stock lenses, kits, frames and pouches in fixed multiples and has **no
  prescription configurator**. It is not the source for this ticket.

**Method.** Plain `curl` of the product page is blocked by the site's Mod_Security ("Not
Acceptable"). I therefore read the page in the desktop app's built-in browser. I took the option
lists straight from the rendered `<select>`/`<input>` elements, and the rules from the page's own
inline JavaScript. I only read the page: I changed no selects, submitted nothing and added nothing
to the cart. Changing a select fires a price lookup (POST `get_prescription_price.php`), so I did
not test pricing and quote behaviour. They are described from the code only.

Field App code compared: `src/DotGlasses.App/Pages/LensRangeSelector.razor` (the Custom
prescription controls), `src/DotGlasses.App/Pages/CoatingMultiSelector.razor`,
`src/DotGlasses.App/Pages/ConsultationForm.razor`, `src/DotGlasses.Rules/ConsultationRules.cs`
(`CustomBranch`, `CustomPower`, `CustomAxis`, `LensType`, `CustomPupilDistance`, `Coatings`), and
the reference-data seed in
`src/DotGlasses.Infrastructure/Persistence/Configurations/ReferenceDataSeedConfiguration.cs`.

## Per-eye entry

Both tools enter the left and right eye separately, with identical option lists for each eye. The
e-commerce page lists **Left Eye first, then Right Eye**. For each eye it asks "Spherical Power",
"Cylindrical Power", "Axis" and "Add Near Vision Power". PD is a single value, not one per eye.

## Sphere (SPH)

| | E-commerce | Field App today |
|---|---|---|
| Range | -10.00 to +10.00 | -10.00 to +10.00 (`SphereSteps`; rule `CustomPower(-10, 10, 0.25)`) |
| Step | 0.25 | 0.25 |
| Control | dropdown, placeholder "Select" | dropdown, placeholder "Select..." |
| Option order | `0.00` first, then -10.00 ascending to 10.00 (0 is not repeated) | -10.00 ascending to +10.00 |
| Label format | Positive values have **no `+`** (e.g. `2.50`); zero is `0.00` | `+2.50`; zero shows as `+0.00` |
| Required | Yes, both eyes: the price shows "-" until both SPH and both CYL are set | Yes, both eyes (`CustomSphereLeft and CustomSphereRight are required…`) |

**Difference:** the range and step match. Only the option order and labels differ.

## Cylinder (CYL)

| | E-commerce | Field App today |
|---|---|---|
| Range | **0.00 to -6.00, negative only** | **-10.00 to +10.00** (`CylinderSteps`; rule `CustomPower(-10, 10, 0.25)`) |
| Step | 0.25 | 0.25 |
| Option order | `0.00` first, then -6.00 ascending to -0.25 | -10.00 ascending to +10.00 |
| Required | Yes, for the price to calculate | Optional |

**Differences:**
- The Field App offers **positive cylinder** values (+0.25 to +10.00). The e-commerce site offers none.
- The Field App goes down to **-10.00**. The e-commerce site stops at **-6.00**.

This confirms the CEO's point.

## Axis

| | E-commerce | Field App today |
|---|---|---|
| Range | 0 to 180 | 0 to 180 (`CustomAxis`) |
| Step | 1° (whole degrees) | 1° (whole degrees) |
| Control | **dropdown** (0, 1, 2 … 180) | **free number input** (`type="number" min=0 max=180 step=1`) |
| Required | **Yes, both eyes**, even when CYL is 0.00. Add to cart / checkout alerts "Please select a left eye axis…" | Optional |

**Difference:** the range and step match, but the control type and whether axis is required
differ. The site does not tie axis to CYL ≠ 0; it simply always requires it.

## Add (near-vision power)

| | E-commerce | Field App today |
|---|---|---|
| Range | 0.00 to 3.00 | 0.00 to +3.00 (`AddPowerSteps`; rule `CustomPower(0, 3, 0.25)`) |
| Step | 0.25 | 0.25 |
| Label format | `0.00`, `0.25` … `3.00`, no `+` | `+0.00` … `+3.00` |
| Required | No. The site's own code notes Add may be left unset | Optional |
| Triggers lens design | when **Add > 0** on either eye | when Add is **non-null** on either eye, **including +0.00** |

**Differences:**
- The range matches. The CEO's "stops at +3.00" is true of **both** tools.
- **Subtle bug-level difference:** in the Field App, choosing `+0.00` as the add power shows and
  requires the Lens type dropdown. `ConsultationRules.LensType` treats any non-null add as "two
  distinct powers". The e-commerce site treats an add of 0.00 as no add.

## Lens type / lens design

| | E-commerce | Field App today |
|---|---|---|
| Options | **Progressive**, **Bifocal** (radio, "Please select one") | **Bifocal**, **Progressive**, **Other** (+ free text) — reference data `LensType` |
| Single vision | Not a choice. It is implied when Add = 0/unset, and the section is hidden | Not a choice. It is implied when no add power is set |
| When shown | Only when left or right Add **> 0**. It is hidden again, and the choice cleared, when both adds go back to 0 | When either add power is non-null (see above) |
| Required when shown | Yes ("Please select a lens design") | Yes (`LensTypeRefId is required when an add power is set…`) |

**Difference:** the Field App has an extra **Other** option. The e-commerce site lists
Progressive before Bifocal.

The page also contains an unused legacy template, `createPrescriptionForm()`, which nothing calls.
It has Single Vision / Bifocal / Progressive radios and a different coating list: Anti glare,
Photochromatic, "Blue block (computer)", "Polarised (driving)", Clear. Customers cannot reach it.
I mention it only because it hints at earlier or planned options.

## Coatings / tints

| E-commerce (checkboxes, multi-select) | Field App seed (`ReferenceDataCategory.Coating`) |
|---|---|
| Photochromatic | Photochromic (spelled differently) |
| Anti-glare | Anti-glare |
| Blue block | Blue block |
| **Yellow tint** | *(absent)* |
| Clear / No coatings (value `Clear`) | Clear |
| *(absent)* | **Polarized** |
| *(absent)* | **Sunglasses** |

Rules on the e-commerce site:
- **At least one coating must be ticked** before add-to-cart or checkout ("Please select at least
  one coating option…"). This matches the Field App's Sale rule "Choose at least one coating."
- **"Clear / No coatings" is mutually exclusive with every other coating.** Ticking Clear
  **unticks and disables** all the others. Ticking any other coating unticks Clear.
- **No auto-add (pairing) rules** exist in the page code.
- There are **no other exclusions**. Photochromatic, Anti-glare, Blue block and Yellow tint can be
  combined freely.
- The coatings are priced: the per-eye price lookup sends a flag for each coating, and `Clear` is
  stripped before the lookup. Some combinations return a "quote" status, and the page then shows
  "Please contact us for a quote". I did not test which combinations do this.
- Coatings are offered for **any** prescription. They are not narrowed by lens type or power.

Field App today:
- The coating list is admin-managed reference data. A Custom prescription accepts **any active
  coating**.
- Pairing and exclusion rules are admin-configured (`CoatingPairings` / `CoatingExclusions`, enforced
  by `CoatingMultiSelector` and `ConsultationRules.Coatings`). **None are seeded in code.** The live
  environments may hold admin-entered rules that this research did not inspect. `CONTEXT.md` gives
  "Clear excludes Photochromic and Sunglasses" as an example, not as seeded data.
- Behaviour differs from the site even where a rule exists. The Field App **blocks** a conflicting
  tick with a message. The e-commerce site **replaces**: ticking Clear clears the others.
- A Test or Lead captures a single optional **coating preference**, not a set. Only a Sale takes the
  multi-select set.

## Pupillary distance (PD)

| | E-commerce | Field App today |
|---|---|---|
| Range | 54 to 74 mm | 54 to 74 mm (`CustomPupilDistance`) |
| Step | 1 mm | 1 mm (whole millimetres) |
| Per eye? | No, a single binocular value | No, a single value |
| Required | Yes. Add to cart / checkout alerts "Please select a pupil distance…" | Required on a **Sale**; optional on a Test or Lead |

**Difference:** none for a Sale.

## Everything else the configurator asks for

- **Frame colour** (required, click a swatch): Black, Blue, Blue Black, Brown Black, Pink, Pink
  Black. The Field App's seeded frame colours already match these six, plus Other.
- **Dot colour** (the rivet colour, changes the preview image): blue, green, orange, pink, yellow,
  white. **The Field App has no dot-colour field.**
- **Quantity.** With quantity > 1, each pair gets its own prescription, lens design, coatings and
  frame/dot colour in an accordion.
- **Alternative path: "Upload Prescription"** instead of choosing values. It takes Full Name, Phone
  (M-Pesa), Email, City, Address and a prescription file (JPG/PNG/PDF), then "Send Prescription".
- Disclaimer shown under PD: the customer is responsible for the accuracy of the uploaded
  prescription, and "Base price includes standard lenses. Additional charges may apply for special
  lens options."
- Hard case upsell (separate product, Ksh 290).
- Pricing is per eye from a server-side price table (`get_prescription_price.php`) keyed on SPH,
  CYL, Add, bifocal/progressive and each coating. **Axis and PD do not affect price.**

## Summary of changes needed to match like for like

1. **CYL:** narrow to 0.00 … -6.00, 0.25 steps, negative only, in both `LensRangeSelector` and
   `ConsultationRules.CustomPower`.
2. **Add = 0.00 must not trigger lens type.** Either treat 0 as "no add" in `ConsultationRules.LensType`
   and in the selector's `@if`, or drop 0.00 from the add list.
3. **Axis:** consider a 0–180 dropdown and making it required, if the CEO wants the site's
   behaviour exactly.
4. **Lens type:** decide whether to keep **Other**, and whether to reorder to Progressive, Bifocal.
5. **Coatings:** add **Yellow tint**. Decide on Polarized and Sunglasses, which are Field App only.
   Rename "Photochromic" to "Photochromatic"? Configure an exclusion between **Clear** and every
   other coating.
6. Optional cosmetic changes: option order (0.00 first) and the `+` sign on labels.

## Open questions

1. **Cylinder required?** The site needs CYL for its price lookup. The Field App treats it as
   optional. Should it be required, or should blank default to 0.00?
2. **Axis required even when CYL = 0.00?** The site always requires it. Clinically it only matters
   when CYL ≠ 0. Copy the site, or require axis only with a non-zero CYL?
3. **"Other" lens type:** keep it? The site offers only Progressive and Bifocal.
4. **Polarized and Sunglasses** are in the Field App but not on the site, and **Yellow tint** is on
   the site but not in the Field App. Is the site's four-coating-plus-Clear list the complete set to
   copy, or does the field sell extra coatings the online shop doesn't?
5. **Coating combinations:** are Photochromatic, Anti-glare, Blue block and Yellow tint really all
   combinable in any mix, as the site allows? Should the Field App block a conflicting tick, as it
   does today, or clear the others when Clear is ticked, as the site does?
6. **Quote-only combinations:** the site's price table flags some SPH/CYL/Add/coating combinations
   as "contact us for a quote". Should the Field App flag or allow them? This needs the price table
   (`get_prescription_price.php`), which is server-side and not visible from the page.
7. **Dot colour:** should the Field App capture it, since the site asks for it?
8. Positive SPH labels: the site shows `2.50`, the Field App `+2.50`. The `+` is clinically clearer.
   Confirm that "like for like" does not mean dropping it.
9. **Live coating rules:** I did not check the admin-configured `CoatingPairings` and
   `CoatingExclusions` in staging or production. Check them before changing anything.
