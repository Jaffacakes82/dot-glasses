# 12 — Event History: lens power columns and the training-org marker

Type: grilling
Status: open
Blocked by: 06

## Question

What lens columns does the Sales tab show once a lens is a full record (left and right eye), and
how is a training org shown on the Outlet column?

## Context

- Feedback doc: add "lens power LE, lens power RE" on Sales; show whether the outlet is a training
  org "to aid data review".
- From ticket 06 (ADR-0007): every record stores each eye's lens power, whether it came from a
  lens set or a custom prescription. The LE/RE columns can read the record directly and show `+`
  on positive values.
