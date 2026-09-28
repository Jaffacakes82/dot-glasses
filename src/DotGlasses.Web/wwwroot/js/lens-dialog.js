// The Lens Sets screen's one Add lens dialog (Views/Catalogues/_LensDialog.cshtml; ADR-0007,
// prototype variant A). "Add lens" opens it blank for that lens set, "Edit" opens it on the lens
// its button carries. What it shows follows the lens power the way the Rules read it: the axis
// only with a cylinder, the lens type only with an add, the "Other" text only with Other, and a
// pairing's dropdowns only offer the ticked coatings. The server checks all of it again on save —
// this only keeps the admin from being asked for things that mean nothing.
(() => {
    const dialog = document.getElementById('lensDialog');
    if (!dialog) {
        return;
    }

    const form = dialog.querySelector('form');
    const rows = form.querySelector('[data-pairing-rows]');
    const template = dialog.querySelector('template[data-pairing-template]');
    const field = name => form.querySelector(`[name="${name}"]`);
    const modal = () => bootstrap.Modal.getOrCreateInstance(dialog);

    // 0.00 cylinder and 0.00 add are "none"; their options carry data-none.
    const isNone = select => !select.value || select.selectedOptions[0]?.hasAttribute('data-none');
    const tickedCoatings = () => [...form.querySelectorAll('input[name="CoatingIds"]:checked')].map(c => c.value);

    function sync() {
        const noCylinder = isNone(field('Cylinder'));
        field('Axis').disabled = noCylinder;
        form.querySelector('[data-axis-note]').hidden = !noCylinder;

        // A disabled control isn't posted, so a hidden lens type or "Other" text never reaches
        // the server to be refused as "must be empty".
        const hasAdd = !isNone(field('Add'));
        form.querySelector('[data-lens-type-section]').hidden = !hasAdd;
        form.querySelector('[data-single-vision-note]').hidden = hasAdd;
        const lensTypes = [...form.querySelectorAll('input[name="LensTypeRefId"]')];
        lensTypes.forEach(radio => radio.disabled = !hasAdd);
        const otherChosen = hasAdd && lensTypes.some(radio => radio.checked && radio.dataset.other === 'true');
        form.querySelector('[data-other-text]').hidden = !otherChosen;
        field('LensTypeOtherText').disabled = !otherChosen;

        // A coating that isn't ticked is hidden from the pairing dropdowns — unless it is the one
        // already chosen, which stays visible so the server's "must be ticked" has something to
        // point at.
        const ticked = tickedCoatings();
        rows.querySelectorAll('select').forEach(select => {
            for (const option of select.options) {
                const off = option.value !== '' && !ticked.includes(option.value) && !option.selected;
                option.hidden = off;
                option.disabled = off;
            }
        });
        form.querySelector('[data-pairing-hint]').hidden = ticked.length >= 2;
    }

    // Model binding reads Pairings[0], Pairings[1]… and stops at the first gap, so the rows are
    // renumbered from 0 after every add and remove.
    function renumber() {
        [...rows.children].forEach((row, index) => {
            row.querySelectorAll('[name]').forEach(el => el.name = el.name.replace(/^Pairings\[[^\]]*\]/, `Pairings[${index}]`));
            row.querySelectorAll('[data-error-for]').forEach(el => el.dataset.errorFor = `Pairings[${index}]`);
        });
    }

    function addRow(trigger = '', paired = '') {
        rows.appendChild(template.content.cloneNode(true));
        const row = rows.lastElementChild;
        row.querySelector('[data-pairing-role="trigger"]').value = trigger;
        row.querySelector('[data-pairing-role="paired"]').value = paired;
        renumber();
    }

    function open(button, lens) {
        form.querySelectorAll('[data-error-for]').forEach(slot => slot.replaceChildren());
        const otherErrors = form.querySelector('[data-other-errors]');
        otherErrors.replaceChildren();
        otherErrors.hidden = true;
        rows.replaceChildren();

        const lensSetName = button.dataset.catalogueName;
        dialog.querySelector('[data-lens-dialog-title]').textContent = lens ? `Edit lens in ${lensSetName}` : `Add lens to ${lensSetName}`;
        field('CatalogueId').value = button.dataset.catalogueId;
        field('LensOptionId').value = lens ? lens.id : '';
        field('Label').value = lens ? lens.label : '';
        field('Sphere').value = lens ? lens.sphere : '';
        // The Edit data holds the option values for "none" (0.00), so a new lens starts on the
        // first option, which is 0.00 for both lists.
        field('Cylinder').value = lens ? lens.cylinder : field('Cylinder').options[0].value;
        field('Axis').value = lens ? lens.axis : '';
        field('Add').value = lens ? lens.add : field('Add').options[0].value;
        form.querySelectorAll('input[name="LensTypeRefId"]').forEach(radio => radio.checked = !!lens && radio.value === lens.lensTypeRefId);
        field('LensTypeOtherText').value = lens?.lensTypeOtherText ?? '';
        form.querySelectorAll('input[name="CoatingIds"]').forEach(box => box.checked = !!lens && lens.coatingIds.includes(box.value));
        (lens?.pairings ?? []).forEach(p => addRow(p.triggerCoatingRefId, p.pairedCoatingRefId));

        sync();
        modal().show();
    }

    document.addEventListener('click', event => {
        const add = event.target.closest('[data-lens-add]');
        if (add) {
            open(add, null);
            return;
        }

        const edit = event.target.closest('[data-lens-edit]');
        if (edit) {
            open(edit, JSON.parse(edit.dataset.lens));
        }
    });

    form.addEventListener('change', sync);
    form.querySelector('[data-pairing-add]').addEventListener('click', () => {
        addRow();
        sync();
    });
    rows.addEventListener('click', event => {
        const remove = event.target.closest('[data-pairing-remove]');
        if (remove) {
            remove.closest('[data-pairing-row]').remove();
            renumber();
            sync();
        }
    });

    // A refused save comes back with the dialog rendered on the admin's own input and every
    // problem in place (CataloguesController.SaveLens) — just show it.
    sync();
    if (dialog.dataset.openOnLoad === 'true') {
        modal().show();
    }
})();
