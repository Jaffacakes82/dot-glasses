// Small behaviours shared by every Admin Portal screen. No behaviour here decides anything the
// server doesn't check again.

// <form data-dg-confirm="..."> asks before it submits. The text is rendered by the server.
document.addEventListener('submit', function (event) {
    var form = event.target;
    var message = form.getAttribute && form.getAttribute('data-dg-confirm');
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// <input data-dg-filter="#list"> narrows the rows of #list (elements carrying
// data-dg-filter-text) as the admin types. A ticked row always stays visible, so a filter can
// never hide something that is about to be saved.
document.addEventListener('input', function (event) {
    var input = event.target;
    var selector = input.getAttribute && input.getAttribute('data-dg-filter');
    if (!selector) {
        return;
    }

    var list = document.querySelector(selector);
    if (!list) {
        return;
    }

    var needle = input.value.trim().toLowerCase();
    list.querySelectorAll('[data-dg-filter-text]').forEach(function (row) {
        var checkbox = row.querySelector('input[type=checkbox]');
        var matches = row.getAttribute('data-dg-filter-text').toLowerCase().indexOf(needle) !== -1;
        row.hidden = !(needle === '' || matches || (checkbox && checkbox.checked));
    });
});
