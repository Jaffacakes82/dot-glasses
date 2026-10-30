# 10 — Export every record's data

Type: grilling
Status: needs-triage
Blocked by: None

## Question

What should a "full" export contain, and in what shape?

## Context

- Feedback: "data export currently only gives the summary data shown on the admin. It should
  export all data e.g. test results, referrals made and why. Etc."
- Confirmed: Event History's CSV (`EventHistoryController.Export`) exports the active tab only,
  with thin columns. Tests: type, outlet, training org, country, created — no age, gender,
  occupation, outcome, lens or referral. Leads and Sales carry name, outlet, lens and a few
  answers, but not age, gender, occupation or referral answers. The Dashboard has no export.

## Points to settle

- Fields: every answer on each record (age, gender, occupation, outcome, lens powers, coatings,
  frame colour, hard case, reason not purchased, aware of price, referral reason/location,
  treated, contact consent)? Labels resolved through `ReferenceDataSnapshot`, retired ones too.
- Links: include the Test → Lead → Sale ids so a journey can be followed?
- Personal data: phones are masked on screen today. Should the export carry full phone numbers
  and names, and if so for whom (DGI only)? Consent flag respected?
- Shape: one CSV per record type (as now, but complete), one combined file, or an Excel workbook
  with a sheet per type?
- Who may export, and is it still bounded by the screen's date range and filters?
