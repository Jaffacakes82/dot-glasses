# 16 — Reference data images: URL or upload

Type: grilling
Status: resolved
Blocked by: None

## Question

What can the image URL field accept today, and should admins upload images instead?

## Context

- Feedback doc, Questions: "Image url - what can it accept? also why no upload."
- Frame colour images are the main consumer (see the frame colours ticket).

## Answer

Decided by grilling, 2026-10-01.

**What the box accepts today**

- Any text up to 2,000 characters. Nothing checks that it is a web address or a picture. The six
  frame colours point at pictures on the online shop's website.

**Upload replaces the web address**

- The Reference Data screen gets a "Choose picture" upload in place of the web address box.
  Pictures go into the existing `reference-data-images` blob container.
- **Upload only.** Keeping a pasted address alongside was considered and dropped: it needs its
  own validation, a rule for a colour that has both, and it can't be promised offline.
- Accepted files: PNG, JPEG or WebP, up to 1 MB. No resizing on the server.
- An admin can remove a picture without adding another. The colour then shows the plain
  placeholder with its name.
- Pictures stay limited to the two frame colour lists (ticket 15).

**Serving the pictures**

- The blob container stays private. The Admin Portal's server hands the pictures out, and no
  sign-in is needed to fetch one.

**Offline**

- The Field App keeps copies of the pictures when it loads reference data, so they show with no
  connection.

**The six existing shop addresses**

- They keep working, online only, until someone replaces each with an upload. Nothing new can be
  entered as an address. DGI should re-upload them before go-live so they work offline.
