# Spec C — Managing users and organisations

Status: resolved
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/` — tickets 03 (editing a user's role and
org assignments), 04 (assigning several users at once), 17 (organisation "Kind"), 18 (deactivating
an org with sub-orgs) and 20 (forgot password). Rules: ADR-0006. Vocabulary: `CONTEXT.md` — **Org
assignment**, **Scope**, **Role**, **Retail point**, **Retailer**.

## Problem Statement

An admin can invite a user, but can't change them afterwards from the User Directory. A role change
has a server endpoint and no form. Org assignments can only be changed on the Organisations screen,
one user at a time, from a dropdown of every user in scope. The invite form's org picker is a flat,
unindented list with a misaligned checkbox and a stale note saying email isn't wired up.

On the Organisations screen:
- "Kind" is a free-text box nobody can explain; nothing reads it.
- The levels show as "Intermediate" and "RetailPoint".
- An organisation with anything beneath it can't be deactivated, and a child can be reactivated
  under a deactivated parent, where it shows as a tree of its own.
- Records from a deactivated organisation lose their name in the reports.

Neither sign-in page has "Forgot password?". A person who forgets theirs must ask an admin.

## Solution

**Edit user page.** Each User Directory row gets an Edit link to a page for role, org assignments
and full name. One Save applies only what the admin changed. A user partly outside the admin's
scope has a read-only role and a count of hidden organisations. An admin can open their own page:
their role is locked, and they can change their own assignments only if their scope doesn't shrink.

**One org picker.** Invite and Edit share an indented tree with each organisation's level, a filter
box and a line explaining what an assignment grants.

**Assign several users.** The Organisations screen's Assign users dialog becomes a filterable
checkbox list of Active users, assigned in one transaction.

**Organisations screen.** Kind is removed. Levels read "Retail Point" and "Retailer/distributor".
Deactivating takes everything beneath it after a confirmation with counts; reactivating restores
what went with it and is refused while the organisation directly above is deactivated. Reports keep
a deactivated organisation's real name, marked "(deactivated)".

**Forgot password.** Both sign-in pages get it. The link goes by email only, to the existing
set-password page, and returns the person to the app they came from.

## User Stories

1. As an admin, I want an Edit link on each User Directory row, so that I can change a user without leaving the directory.
2. As an admin, I want to change a user's role, org assignments and full name on one page with one Save, so that a swap of their only org is a single step.
3. As an admin, I want my save to apply only what I changed, so that I don't undo a change another admin made a minute earlier.
4. As a country admin, I want a user who also has organisations outside my scope to show a read-only role and "plus N organisations outside your scope", so that I understand why I can't change everything.
5. As a country admin, I want to remove every assignment of a user that I can see, so that I can record that they have left my area; and to be told they are no longer in my scope.
6. As an admin, I want a confirmation before removing a retail-point assignment or demoting an Admin, naming the consequence, so that I don't cut off a technician with unsent records by accident.
7. As an admin, I want to edit Invited and Suspended users too, so that I can correct a mis-invited user before they sign in.
8. As an admin, I want to open my own Edit page to fix my name, so that I don't need another admin for that.
9. As an admin, I want to be unable to change my own role or suspend myself, so that the last admin can't lock themself out.
10. As a DGI admin, I want to assign myself to a retail point, so that I can record there in the Field App.
11. As an admin, I want to be refused when removing one of my own assignments would shrink my scope, so that I can't lose access I can't restore.
12. As an admin inviting or editing a user, I want the organisations shown as an indented tree with each one's level and a filter box, so that I can find the right one among dozens.
13. As an admin, I want a line under the picker saying only a direct retail-point assignment allows recording in the Field App, so that I assign technicians correctly.
14. As an admin, I want a deactivated organisation not offered for a new assignment, and an existing assignment to one marked "deactivated", so that I see why it gives no access.
15. As an admin, I want the directory's column headed "Organisations", so that the screen matches what the word "scope" means elsewhere.
16. As an admin on the Organisations screen, I want to tick several users and assign them all at once, so that staffing a new retail point is one action.
17. As an admin, I want only Active users not already assigned offered there, each with their role, so that the list is short and I see what I'm granting.
18. As an admin, I want a line in that dialog saying what the assignment grants at this level, so that I know whether it allows recording.
19. As an admin, I want a confirmation before unassigning someone from a retail point, so that it matches the Edit page.
20. As an admin, I want the Kind box gone, so that the Add dialog asks only what matters.
21. As an admin, I want levels to read "Retail Point" and "Retailer/distributor", so that they read as words.
22. As an admin, I want to deactivate an organisation and everything beneath it in one action, after seeing how many organisations and people are affected, so that closing a retailer isn't dozens of clicks.
23. As an admin, I want reactivating to bring back what was deactivated with it and leave alone anything deactivated separately, so that an earlier closure isn't reversed by accident.
24. As an admin, I want to be refused when reactivating an organisation whose parent is still deactivated, so that no organisation floats outside the tree.
25. As an admin, I want the Deactivated orgs strip to list each deactivated group once with a count, so that it stays readable.
26. As anyone reading a report, I want records from a deactivated organisation to show its real name with "(deactivated)", so that the data is still attributable.
27. As a user who forgot my password, I want a "Forgot password?" link on the Admin Portal and Field App sign-in pages, so that I don't have to ask an admin.
28. As that user, I want to be returned to the app I came from after setting a new password, so that a technician doesn't land on the Admin Portal.
29. As DGI, I want the same message shown whether or not an email has an account, no email sent to a suspended account, and at most one email per account every five minutes, so that the feature can't be used to probe or flood.
30. As DGI, I want each change to a user's role or assignments written to the application log with who made it, so that there is some trace.

## Implementation Decisions

**Edit user page**
- A dedicated page off the User Directory; a refused save redirects back to it (the existing
  `DomainRuleViolationFilter` behaviour).
- The form carries the role and the assignment set it was loaded with. The server computes the
  differences and applies only those, in one transaction opened through the execution strategy, as
  `UserAdminService.InviteAsync` does. Every `IdentityResult` is checked.
- Permission per change, unchanged from ADR-0006: a role or name change needs every assignment of
  the target in scope (`Users.ManageInScope`); adding or removing one assignment needs that org in
  scope (`Organisations.ManageInScope`).
- The page is reachable for any user the admin can see. Fields the admin can't change are
  read-only with a reason. Out-of-scope assignments are counted, never named, and never touched.
- "At least one assignment" counts assignments the admin can't see.
- Email is not editable.
- A user with no assignment at all is listed for DGI-level admins only, as "No organisation"
  (2026-10-01). The Edit page opens for such a user, and saving at least one assignment repairs
  them.
- A confirmation is shown before saving when a retail-point assignment is removed or an Admin
  becomes a User.
- Row actions stay on the directory: Edit, Reset password, Suspend/Unsuspend.

**Editing yourself (both screens)**
- Own role: refused. Self-suspend: refused. Both are `DomainRuleViolationException`s.
- Own name: allowed.
- Adding your own assignment: allowed (the org is already in your scope).
- Removing your own assignment: allowed only when your scope afterwards is no smaller, which means
  the removed org sits beneath another assignment you keep. Evaluate on the resulting set.

**Org picker (Invite and Edit)**
- One shared partial: an indented tree built from the admin's visible organisations, each with its
  level label, a client-side filter box, and the help line.
- Nested picks are allowed. Deactivated orgs are not offered; an existing assignment to one is
  shown ticked, marked "deactivated" and removable.
- The invite dialog's "No email sending is wired up yet" note is removed.
- The directory's "Scope" column is renamed "Organisations".

**Assign users dialog**
- A checkbox list with a client-side filter, posting a list of user ids for one org. One
  transaction; all or nothing.
- Offers Active users only, excluding those already assigned to the org.
- Shows each user's role in the dialog and in the Assigned users list.
- A level-dependent line under the title says what the assignment grants.
- Unassign stays one user at a time and confirms at a retail point.

**Organisations screen**
- `OrganisationNode.Kind` is removed from the entity, the Add dialog, the selected-node panel, the
  CSV export, the seed data and the database.
- Level labels are produced in one place and used wherever a level is shown, including the CSV:
  "DGI", "Country", "Retailer/distributor", "Retail Point".
- Deactivate soft-deletes the organisation and all descendants in one transaction. The group is
  recorded so that reactivation can restore exactly that group; a descendant already deactivated
  beforehand is not part of it.
- The confirmation states the number of organisations, the number of people who lose access,
  and that unsent Field App records for those retail points will be refused.
- Reactivate is refused when the parent is deactivated. Both the authorization lookup and the
  mutation need `IgnoreQueryFilters()` (see CLAUDE.md's pitfalls).
- The Deactivated orgs strip lists each group's top organisation with a count and one Reactivate
  button.
- The reports' organisation lookup (`IUnscopedReportQueryService`, `OrgTreeLookup`) includes
  deactivated organisations and marks their names "(deactivated)". Their data keeps counting.

**Forgot password**
- Admin Portal: an anonymous page taking an email address. Field App: an anonymous API endpoint
  with a small screen calling it directly (it needs a connection and is not an outbox write).
- Both always answer with the same message.
- An email is sent for an Active or Invited account, not for a Suspended one, and not within five
  minutes of the last one. The last-sent time is stored on the user row.
- A new `IEmailSender` method with its own wording, stating the link works for 24 hours. The link
  targets the existing set-password page and carries which app asked; after a successful reset the
  page redirects to that app's sign-in. The Field App's address comes from configuration.
- The link is never shown on screen.

**Logging**
- Each role change, name change, assignment add or remove (from either screen) writes one
  structured application log entry: acting user, target user, what changed.

## Testing Decisions

- **Seam: `DotGlasses.Web.Tests`** over HTTP against the containerised Postgres, asserting on what
  the admin sees: status codes, redirects, rendered content. Prior art: `UserDirectoryScopeTests`,
  `AccessControlPolicyTests`, `InviteAtomicityTests`, `DomainRuleViolationScreenTests`.
- **Cases to cover:**
  - Edit: role, name and assignments saved together; a swap of the only assignment succeeds; a
    stale form doesn't remove an assignment added since; a partly-out-of-scope user's role change
    is refused; removing all visible assignments succeeds when one remains elsewhere and is refused
    when none does.
  - Self: own role change and self-suspend refused; self-add accepted; self-remove accepted when
    covered by another assignment and refused when it would shrink scope, on both screens.
  - Picker: a deactivated org isn't offered; an existing assignment to one is listed.
  - Bulk assign: several users assigned in one request; a Suspended or Invited user id is refused;
    one bad id rolls back the whole batch.
  - Organisations: Kind is gone from the page and the CSV; level labels read as specified.
  - Deactivation: a parent with children deactivates the whole group; reactivating restores the
    group and leaves a separately deactivated child off; reactivating under a deactivated parent
    is refused with its message; a deactivated organisation's records still count on the dashboard
    and show its name with "(deactivated)".
  - Forgot password: the same response for a known, unknown and suspended address; one email for
    an active account; none for a suspended one; none within five minutes; the set-password page
    redirects to the right app.
- **`AccessAuditTests`** audits every controller action for three kinds of caller. Every new
  action here (the Edit page, bulk assign, forgot password) is added to it.
- **`Application.Tests`** may unit-test any pure helper, such as "does this removal shrink the
  scope", with no new dependencies.
- **Field App** forgot-password screen is checked by hand; there is no Field App test project.

## Out of Scope

- A change-history screen for user edits (noted in `docs/open-issues.md`).
- Editing a user's email.
- A self-service account page beyond what the Edit page gives.
- Assigning several users to several orgs in one submit; bulk unassign.
- Transferring an organisation to a different parent (Day 2).
- An "organisation type" reference list, and a retail-point type.
- A `dotglasses.com` email sender (in `docs/open-issues.md`, waiting on the owner).
- One role per assignment.

## Further Notes

- **Email reliability.** Forgot password has no on-screen fallback. Until the sender moves off
  Azure's shared domain, reset emails are more likely to land in spam, and the admin's Reset
  password button remains the fallback.
- **Level labels feed the picker**, so ticket 05 lands before ticket 03 or its labels are applied
  there afterwards.
- **Accepted race.** Two admins removing a user's last two assignments at once is already listed
  in `docs/open-issues.md`; the diff-based save doesn't change that.
