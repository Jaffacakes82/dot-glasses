# 02 — Integration test harness on real Postgres

**What to build:** Integration tests run against a real containerised Postgres instead of the
in-memory provider, so that behaviour depending on transactions and on real SQL string matching can
be tested at all. No production behaviour changes. This is a prefactor: it exists to make later
tickets testable.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent
**Category:** chore

- [ ] Infrastructure and Web integration tests execute against a containerised Postgres
- [ ] The existing hierarchy query-filter and audit-interceptor tests pass unchanged against it
- [ ] The existing widget API tests pass unchanged against it
- [ ] A test can open a transaction and assert rollback behaviour
- [ ] Pure Application-layer tests remain free of any database or container dependency
- [ ] The suite runs in CI without additional manual setup
