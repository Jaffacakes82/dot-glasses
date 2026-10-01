# 17 — Organisation "Kind" as a managed dropdown

Type: grilling
Status: resolved
Blocked by: None

## Question

What is an organisation's "Kind" for, and should it become a managed dropdown rather than free
text?

## Context

- Feedback doc: "what is 'kind' and could this be in the preset catalogue and a drop down?"
- Also: the level label "RetailPoint" should read "Retail Point".
- Today: free-text display label on `OrganisationNode`, `Intermediate` level only.

## Answer

Decided by grilling, 2026-10-01.

**Kind is removed, not turned into a dropdown**

- Kind was an optional free-text label set when an organisation was created. No rule, report or
  filter reads it, it couldn't be changed afterwards, and the Add dialog asked for it at every
  level. It isn't needed.
- It goes from the Add dialog, from the line under the organisation's name on the Organisations
  screen, from that screen's CSV export, from the seed data and from the database.
- No "organisation type" reference data list is added.

**Level labels**

- "RetailPoint" becomes **"Retail Point"** wherever a level is shown, including the CSV export.
- "Intermediate" becomes **"Retailer/distributor"**, the wording the Add button already uses for
  that level.
- "DGI" and "Country" are unchanged. The glossary's **Retailer** keeps its meaning.

**Note for the spec**

- `CLAUDE.md` describes `Intermediate` as covering every reseller tier "via a free-text `Kind`
  label". That sentence goes when this is built.
