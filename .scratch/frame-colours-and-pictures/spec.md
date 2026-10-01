# Spec E — Frame colours and their pictures

Status: ready-for-agent
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/` — tickets 15 (separate frame colours for
adult and child frames) and 16 (reference data images: URL or upload).

## Problem Statement

A Sale records the frame's colour, picked from a row of pictures. There is one list of colours, but
adult and children's frames come in different colours with different pictures. The form already
asks "children's frame" and ignores the answer when offering colours.

Each colour's picture is a web address an admin pastes into a text box. The box accepts any text.
The six current pictures point at the online shop's website, so a change there breaks them. The
Field App fetches each picture when the form is shown and keeps no copy, so offline the pictures
are missing. Storage for uploads has existed in Azure since the infrastructure was set up; nothing
uses it.

## Solution

**Two lists.** Reference data has "Frame colours (adult)" and "Frame colours (child)", each with
its own pictures and its own "Other". The current colours become the adult list. The child list
starts with "Other" only, for DGI to fill in.

**The forms follow the tick.** The Field App's Sale form and the Admin Portal's Lead conversion
form offer the list that matches "children's frame", and the server refuses a colour from the
wrong list.

**Upload replaces the web address.** An admin uploads a picture on the Reference Data screen. It
goes into the existing private blob container and the Admin Portal serves it. The Field App keeps
copies so pictures show offline.

## User Stories

1. As a DGI admin, I want separate adult and child frame colour lists on the Reference Data screen, so that each has the right colours and pictures.
2. As a technician recording a Sale, I want the colours offered to match whether it is a children's frame, so that I can't pick a colour that frame doesn't come in.
3. As a technician, I want my colour choice cleared when I change the "children's frame" tick, so that a colour from the other list isn't saved by accident.
4. As an admin converting a Lead on the Admin Portal, I want the same behaviour, so that both ways of recording a Sale agree.
5. As DGI, I want the server to refuse a colour from the wrong list, so that the rule holds whatever the client sends.
6. As anyone reading Event History, I want Sales recorded before this change to keep showing their colour, so that nothing is lost.
7. As a DGI admin, I want to upload a picture for a frame colour, so that I don't have to host it somewhere and paste an address.
8. As a DGI admin, I want to remove a colour's picture, so that a wrong one can be taken down without a replacement.
9. As a DGI admin, I want an upload of the wrong type or size refused with a clear message, so that I know what to change.
10. As a technician working offline, I want the frame pictures to show, so that I can pick by sight as I do online.
11. As DGI, I want the storage to stay closed to the internet, so that nothing new is exposed.

## Implementation Decisions

**Two categories**
- `ReferenceDataCategory` gains a child frame colour value. The existing `FrameColour` value keeps
  its number and becomes the adult list; only its label changes. Do not reuse the retired Lens
  strengths value.
- The Reference Data screen shows both lists, both with the picture field. Each may have one
  active "Other" item, as the existing rule allows per category.
- A migration seeds the child list's "Other" item. The adult colours are deliberately not copied.
- `Contracts` carries its own copy of the category enum where one exists; keep them in step.

**The rule**
- `ConsultationRules.FrameColour` takes the Sale's `ChildrensFrame` and requires the colour to be
  an active item of the matching category. The failure stays keyed on `FrameColourRefId`.
- Sales already recorded are untouched; labels resolve by id through `ReferenceDataSnapshot`,
  retired items included.
- `SaleAssembly` is unchanged: frame colour is a point-of-sale answer and is not carried from a
  Lead.

**The forms**
- Field App: `RenderFrameSwatches` reads the category from the "children's frame" tick. Changing
  the tick clears a chosen colour and its "Other" text. The pre-submit check uses the shared rule.
- Admin Portal's conversion form: the colour list is rendered from Rules for both categories and
  the existing script shows the matching one; without the script the server validates and
  re-renders (the form's existing progressive-enhancement pattern).
- The Field App's cached reference data gains a category. A cache written before this ships has
  no child list. With "children's frame" ticked and no child list cached, the form offers no
  colours and says to connect to load them; it must never fall back to the adult list. No cache
  shape marker is bumped, because an added category doesn't change what the cached adult list
  means.

**Upload**
- The Reference Data screen's create and edit forms take a file in place of the address box. PNG,
  JPEG or WebP, up to 1 MB, checked by content type and by reading the file's signature, not by
  its extension. No resizing.
- A new Application interface for picture storage, implemented in Infrastructure over the
  `reference-data-images` blob container, which AppHost already provisions and references from
  `Web`. Locally it runs on the Azurite emulator. Tests use a hand-written in-memory fake.
- Each upload is stored under a new generated name; the item's `ImageUrl` then holds the address
  the Admin Portal serves it from. Replacing or removing a picture deletes the old blob.
- "Remove picture" clears the item's picture.
- The six existing shop addresses are left in place and keep working online. No new address can be
  entered.
- The create and update validators replace the 2,000-character address rule with the file rules.

**Serving**
- An anonymous GET endpoint on `Web` streams a picture from the private container by its generated
  name, with a long cache lifetime (names never repeat). It accepts only names matching the
  generated pattern, so it can't be used to read anything else.
- The container has no public access. Nothing changes in `/infra` beyond what AppHost already
  declares; if AppHost does change, regenerate with `azd infra gen --force`.

**Offline**
- When the Field App loads reference data, it also fetches each frame colour's picture and stores
  it on the device, write-through like the lists. Offline, the swatches use the stored copies.
- A picture that can't be fetched (one of the old shop addresses, or a failure) falls back to the
  existing placeholder; it never blocks the form.

## Testing Decisions

- **`DotGlasses.Rules.Tests`**: the frame colour rule — an adult colour on an adult frame accepted; a
  child colour on an adult frame refused; the reverse; "Other" from the right list with its text
  accepted.
- **`DotGlasses.Web.Tests`** over HTTP: the Sale create endpoint refuses a colour from the wrong
  list keyed on `FrameColourRefId`; the conversion form does the same; the Reference Data screen
  lists both categories; an upload of an accepted type is stored and served back byte for byte; an
  oversized file and a non-picture file are refused with their messages; removing a picture clears
  it; the serving endpoint returns 404 for a name that doesn't match the pattern.
- **`AccessAuditTests`** audits every controller action for three kinds of caller. Every new
  action in this spec is added to it.
- **Field App** swatches following the tick and pictures showing offline are checked by hand, with
  the checklist in the ticket's Comments.
- No mocking library; the storage fake is hand-written.

## Out of Scope

- Recording the frame's dot colour.
- Pictures for any other reference list.
- Entering or keeping a pasted web address for new pictures.
- Resizing or cropping uploads.
- Moving the six existing shop pictures into storage automatically; DGI re-uploads them.
- The children's colours themselves; DGI enters them.

## Further Notes

- **Before go-live.** Until DGI enters the children's colours, a children's frame can only be sold
  with the colour "Other". Until DGI re-uploads the six adult pictures, those load from the online
  shop and don't show offline. Both are in the map's "Before go-live" note.
- **Wording.** New messages follow the voice in Spec D.
- **`docs/open-issues.md`** has "No upload feature for reference-data images"; ticket 05 removes it.
