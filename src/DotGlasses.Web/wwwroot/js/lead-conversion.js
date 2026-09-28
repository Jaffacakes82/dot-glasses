// The Lead conversion screen's lens section (Views/LeadConversion/Convert.cshtml; ADR-0007), kept in
// step with the Field App's: "Same lens for both eyes", the right eye limited to the left eye's lens
// type, a power line under each lens, the axis only with a cylinder, the lens type only with an add,
// and the coatings both lenses come in with a pairing's coating ticked and locked.
//
// This only saves the admin from being asked for things that mean nothing. The server renders the
// same state itself and checks every rule again on submit (ConsultationRules), so the screen still
// works — and a refusal still lands on its control — with this script off. What each pair of lenses
// offers is asked of the server (LeadConversionController.Coatings) rather than worked out here.
(() => {
    const form = document.querySelector('[data-lens-conversion]');
    if (!form) {
        return;
    }

    const one = (selector, root = form) => root.querySelector(selector);
    const all = (selector, root = form) => [...root.querySelectorAll(selector)];

    const lensSets = JSON.parse(one('[data-lens-sets]').textContent);
    const rangeSelect = one('[data-range-select]'); // absent when the Lead's own lens carries over
    const coatings = one('[data-coatings]');

    const customRange = 'custom';
    const field = name => one(`[name="${name}"]`);
    const hasValue = select => !!select && select.value !== '';
    // 0.00 is "none" for a cylinder and an add; the value is in the request's culture, so a comma
    // decimal is read too.
    const isNone = select => !select || select.value === '' || parseFloat(select.value.replace(',', '.')) === 0;

    // ---- What the lens range and the choices so far say is showing ----

    const currentSet = () => lensSets.find(s => s.id === rangeSelect?.value);
    const lensIn = (set, id) => set?.lenses.find(l => l.id === id);
    const isCustom = () => rangeSelect?.value === customRange;
    const sameLens = () => !!one('[data-same-lens]')?.checked;
    const eyeSelect = (role, eye) => one(`[data-${role}="${eye}"]`);

    // A region is on when it applies to what is chosen; an off region is hidden and its controls
    // disabled, so nothing hidden is posted for the server to refuse as "must be empty". Outer
    // regions are applied first, so a nested one's own state wins for its own controls.
    function region(selector, active) {
        all(selector).forEach(el => {
            el.hidden = !active;
            all('input, select, textarea', el).forEach(control => control.disabled = !active);
        });
    }

    function sync() {
        if (!rangeSelect) {
            return;
        }

        const setActive = !!currentSet();
        const customActive = isCustom();
        const separate = setActive && !sameLens();

        region('[data-set-block]', setActive);
        region('[data-set-pd]', setActive);
        region('[data-custom-block]', customActive);
        region('[data-custom-pd]', customActive);
        region('[data-right-lens-field]', separate);

        for (const eye of ['left', 'right']) {
            region(`[data-axis-field="${eye}"]`, customActive && !isNone(eyeSelect('cylinder', eye)));
        }

        const hasAdd = !isNone(eyeSelect('add', 'left')) || !isNone(eyeSelect('add', 'right'));
        region('[data-lens-type-field]', customActive && hasAdd);
        const otherChosen = all('input[name="Form.LensTypeRefId"]').some(r => r.checked && r.dataset.other === 'true');
        region('[data-other-text]', customActive && hasAdd && otherChosen);

        // "Lens" when one dropdown stands for both eyes, "Lens — left eye" when there are two.
        all('[data-label-same]').forEach(el => el.hidden = !sameLens());
        all('[data-label-separate]').forEach(el => el.hidden = sameLens());
        // A left/right/same-lens change can change which of the Lead's notes is the one to show.
        renderLensLines();

        // A children's frame narrows the pupil distance to 0-2.
        const childrens = !!one('[data-childrens-frame]')?.checked;
        const bucket = one('[data-pd-bucket]');
        if (bucket) {
            for (const option of bucket.options) {
                const off = childrens && Number(option.value) > 2 && option.value !== '';
                option.hidden = off;
                option.disabled = off;
            }

            if (childrens && Number(bucket.value) > 2) {
                bucket.value = '';
            }

            const max = one('[data-pd-max]');
            if (max) {
                max.textContent = childrens ? '2' : '4';
            }
        }
    }

    // ---- The lens dropdowns ----

    let notesStale = false; // the Lead's "no longer in the lens set" notes belong to the Lead's own set

    function fillLenses(select, lenses, keepId) {
        select.replaceChildren(new Option('Select...', ''), ...lenses.map(l => new Option(l.label, l.id)));
        select.value = lenses.some(l => l.id === keepId) ? keepId : '';
    }

    // The right eye is limited to the left eye's lens type — a pair has one, and a mixed pair can't
    // be sold. Every lens is offered until the left is chosen.
    function fillRightChoices() {
        const set = currentSet();
        const right = one('[data-lens-select="right"]');
        if (!set || !right) {
            return;
        }

        const left = lensIn(set, one('[data-lens-select="left"]').value);
        const choices = left ? set.lenses.filter(l => l.type === left.type) : set.lenses;
        fillLenses(right, choices, right.value);
        const hint = one('[data-right-hint]');
        if (hint) {
            hint.hidden = !left;
        }
    }

    function renderLensLines() {
        const set = currentSet();
        for (const eye of ['left', 'right']) {
            const select = one(`[data-lens-select="${eye}"]`);
            const lens = lensIn(set, eye === 'right' && sameLens() ? one('[data-lens-select="left"]').value : select?.value);

            const power = one(`[data-power-line="${eye}"]`);
            if (power) {
                power.textContent = lens?.power ?? '';
                power.hidden = !lens;
            }

            const note = one(`[data-lens-note="${eye}"]`);
            if (note) {
                const text = eye === 'left' && sameLens() ? note.dataset.noteSame : note.dataset.noteSeparate;
                note.textContent = text ?? '';
                note.hidden = notesStale || !text || !!select?.value;
            }
        }
    }

    // ---- The coatings both lenses come in ----

    let pairings = JSON.parse(coatings?.dataset.pairings || '[]');
    let request = 0;
    const items = () => all('[data-coating-item]', coatings);
    const boxOf = item => one('input[type="checkbox"]', item);
    const labelOf = id => one(`[data-coating-id="${id}"] label`, coatings)?.textContent.trim() ?? '';

    // The pair the coatings are scoped by, or null when there is none yet — a Custom prescription, or
    // a lens still to choose — when every coating is listed and the server decides on submit.
    function chosenPair() {
        const set = currentSet();
        const left = one('[data-lens-select="left"]')?.value;
        const right = sameLens() ? left : one('[data-lens-select="right"]')?.value;
        return set && lensIn(set, left) && lensIn(set, right) ? { left, right } : null;
    }

    async function refreshCoatings() {
        if (!coatings || !rangeSelect) {
            return;
        }

        const pair = chosenPair();
        if (!pair) {
            request++;
            applyOffered(null, []);
            return;
        }

        const mine = ++request;
        try {
            const response = await fetch(`${coatings.dataset.url}?left=${pair.left}&right=${pair.right}`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
            if (!response.ok) {
                return;
            }

            const data = await response.json();
            if (mine === request) {
                applyOffered(data.offered, data.pairings ?? []);
            }
        } catch {
            // Offline or failed: leave the list as it is — the server checks the coatings on submit.
        }
    }

    // A lens change that takes a coating off the offered list unticks it, and says so.
    function applyOffered(offered, pairs) {
        pairings = pairs;
        const removed = [];
        for (const item of items()) {
            const id = item.dataset.coatingId;
            const isOffered = offered === null || offered.includes(id);
            const box = boxOf(item);
            item.hidden = !isOffered;
            box.disabled = !isOffered;
            if (!isOffered && box.checked) {
                box.checked = false;
                removed.push(labelOf(id));
            }
        }

        const removedNote = one('[data-coatings-removed]', coatings);
        removedNote.textContent = removed.length ? `${removed.join(', ')} ${removed.length === 1 ? "isn't" : "aren't"} offered on the chosen lens, so ${removed.length === 1 ? 'it was' : 'they were'} unticked.` : '';
        removedNote.hidden = removed.length === 0;

        const none = one('[data-coatings-note]', coatings);
        none.textContent = offered !== null && offered.length === 0 ? coatings.dataset.noneOffered : '';
        none.hidden = !none.textContent;

        applyLocks();
    }

    // Ticking a trigger ticks its paired coating and locks it ("Comes with <trigger> on this lens");
    // a pairing chains, so this runs to a fixed point. Unticking the trigger frees the paired one,
    // which stays as ticked as the admin left it.
    function applyLocks() {
        for (const item of items()) {
            boxOf(item).dataset.locked = 'false';
            const note = one('[data-lock-note]', item);
            note.hidden = true;
            note.textContent = '';
        }

        let changed = true;
        while (changed) {
            changed = false;
            for (const { trigger, paired } of pairings) {
                const triggerItem = one(`[data-coating-id="${trigger}"]`, coatings);
                const pairedItem = one(`[data-coating-id="${paired}"]`, coatings);
                if (!triggerItem || !pairedItem || !boxOf(triggerItem).checked || boxOf(pairedItem).disabled) {
                    continue;
                }

                const box = boxOf(pairedItem);
                if (!box.checked) {
                    box.checked = true;
                    changed = true;
                }

                if (box.dataset.locked !== 'true') {
                    box.dataset.locked = 'true';
                    const note = one('[data-lock-note]', pairedItem);
                    note.textContent = `Comes with ${labelOf(trigger)} on this lens`;
                    note.hidden = false;
                }
            }
        }
    }

    // ---- Events ----

    coatings?.addEventListener('click', event => {
        if (event.target.matches('input[type="checkbox"]') && event.target.dataset.locked === 'true') {
            event.preventDefault();
        }
    });
    coatings?.addEventListener('change', event => {
        if (event.target.matches('input[type="checkbox"]')) {
            applyLocks();
        }
    });

    if (rangeSelect) {
        rangeSelect.addEventListener('change', () => {
            // Switching range drops whatever belonged to the other one.
            notesStale = true;
            const set = currentSet();
            const left = one('[data-lens-select="left"]');
            fillLenses(left, set?.lenses ?? [], '');
            fillLenses(one('[data-lens-select="right"]'), set?.lenses ?? [], '');
            all('[data-custom-block] select, [data-custom-pd] select, [data-set-pd] select').forEach(s => s.value = '');
            all('input[name="Form.LensTypeRefId"]').forEach(r => r.checked = false);
            const other = field('Form.LensTypeOtherText');
            if (other) {
                other.value = '';
            }

            sync();
            refreshCoatings();
        });

        one('[data-same-lens]')?.addEventListener('change', () => {
            const left = one('[data-lens-select="left"]');
            const right = one('[data-lens-select="right"]');
            if (!sameLens() && hasValue(left) && !hasValue(right)) {
                // Unticking starts both eyes on the lens already chosen; the admin changes one.
                fillRightChoices();
                right.value = left.value;
            }

            fillRightChoices();
            sync();
            refreshCoatings();
        });

        one('[data-lens-select="left"]')?.addEventListener('change', () => {
            fillRightChoices();
            sync();
            refreshCoatings();
        });

        one('[data-lens-select="right"]')?.addEventListener('change', () => {
            sync();
            refreshCoatings();
        });

        for (const eye of ['left', 'right']) {
            eyeSelect('cylinder', eye)?.addEventListener('change', () => {
                if (isNone(eyeSelect('cylinder', eye))) {
                    const axis = field(`Form.Axis${eye === 'left' ? 'Left' : 'Right'}`);
                    if (axis) {
                        axis.value = '';
                    }
                }

                sync();
            });

            eyeSelect('add', eye)?.addEventListener('change', () => {
                if (isNone(eyeSelect('add', 'left')) && isNone(eyeSelect('add', 'right'))) {
                    all('input[name="Form.LensTypeRefId"]').forEach(r => r.checked = false);
                    const other = field('Form.LensTypeOtherText');
                    if (other) {
                        other.value = '';
                    }
                }

                sync();
            });
        }

        all('input[name="Form.LensTypeRefId"]').forEach(r => r.addEventListener('change', sync));
        one('[data-childrens-frame]')?.addEventListener('change', sync);
    }

    sync();
})();
