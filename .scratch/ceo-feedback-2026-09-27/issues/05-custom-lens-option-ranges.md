# 05 — The lens option ranges the Dot Glasses e-commerce site offers

Type: research
Status: resolved
Blocked by: None

## Question

What sphere, cylinder, axis and add ranges and steps, and which lens types, does the Dot Glasses
e-commerce custom-lens configurator offer? The Field App's custom lens should copy it like for like.

## Context

- Call: the Field App offers positive cylinder powers; the e-commerce site only offers negative
  cylinder (starting at 0), which is the standard notation. "Copy like for like the e-commerce website."
- Call: sphere stops at -10 and add at +3 today, and stronger options may be added later.
- Call: lens type is single vision by default; a lens with an add is bifocal or progressive; "Other"
  covers rare types.
- Feeds the lens identity decision: whether the lens database is an enumerated list or typed values.

## Answer

Findings: branch `research/custom-lens-option-ranges` (commit `796fc36`, not pushed), file
`.scratch/ceo-feedback-2026-09-27/research/custom-lens-option-ranges.md`. Source: the shop's
prescription configurator on dotglasses.org, its only glasses product.

| Field | E-commerce site | Field App today |
|---|---|---|
| Sphere | -10.00 to +10.00, 0.25 steps | same range |
| Cylinder | 0.00 to -6.00, 0.25 steps (negative only) | -10.00 to +10.00, including positive values |
| Axis | 0–180, 1° steps, a dropdown, required for both eyes even when cylinder is 0 | a free number box, optional |
| Add | 0.00 to 3.00, 0.25 steps | same range |
| Lens type | Progressive / Bifocal, asked only when add is above 0 | adds "Other"; also asked when add is +0.00, which is the bug the feedback doc reports |
| Coatings | Photochromatic, Anti-glare, Blue block, Yellow tint, "Clear / No coatings"; at least one required; Clear disables the rest; nothing is added automatically | no Yellow tint; has Polarized and Sunglasses |
| PD | 54–74 mm, 1 mm steps, one value, required | matches on a Sale |

The site also has three things the Field App lacks: a dot colour choice, an option to upload a
prescription instead, and "contact us for a quote" for some combinations.

Open questions carried to "What a lens is" and "How the Field App captures a lens":
- Should cylinder be required?
- Is axis needed when cylinder is 0?
- Should "Other" stay as a lens type?
- Which coating list is the real one?
