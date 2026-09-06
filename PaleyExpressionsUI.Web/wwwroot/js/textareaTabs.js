window.enableTabInTextarea = (function () {
    return function (id) {
        try {
            var el = document.getElementById(id);
            if (!el) return;
            // avoid double-binding
            if (el.__tabHandlerAttached) return;

            var handler = function (e) {
                // Support both modern (e.key) and legacy (e.keyCode)
                var isTab = (e.key === 'Tab' || e.keyCode === 9);
                if (isTab) {
                    // Prevent default focus change and stop other handlers from intercepting
                    if (e.preventDefault) e.preventDefault();
                    if (e.stopPropagation) e.stopPropagation();
                    if (e.stopImmediatePropagation) try { e.stopImmediatePropagation(); } catch (ex) { }

                    var start = el.selectionStart;
                    var end = el.selectionEnd;
                    var value = el.value;

                    // If a range is selected and contains newlines, indent each selected line
                    var selected = value.substring(start, end);
                    if (start !== end && selected.indexOf('\n') !== -1) {
                        var before = value.substring(0, start);
                        var after = value.substring(end);
                        var lines = selected.split('\n');
                        for (var i = 0; i < lines.length; i++) lines[i] = '\t' + lines[i];
                        var newSel = lines.join('\n');
                        el.value = before + newSel + after;
                        // set selection to encompass the newly indented block
                        el.selectionStart = start;
                        el.selectionEnd = start + newSel.length;
                    } else {
                        // Insert a single tab character
                        el.value = value.substring(0, start) + '\t' + value.substring(end);
                        el.selectionStart = el.selectionEnd = start + 1;
                    }

                    // trigger input event so Blazor picks up the change
                    var ev;
                    try { ev = new Event('input', { bubbles: true }); }
                    catch (ex) { ev = document.createEvent('Event'); ev.initEvent('input', true, true); }
                    el.dispatchEvent(ev);

                    // After Blazor potentially updates the DOM, restore caret position
                    // Use setTimeout 0 so this runs after the current event loop and any sync handlers
                    (function (s, epos) {
                        setTimeout(function () {
                            try {
                                el.selectionStart = s;
                                el.selectionEnd = epos;
                                el.focus();
                            } catch (ex) { }
                        }, 0);
                    })(el.selectionStart, el.selectionEnd);
                }
            };

            // Attach with capture so it runs before other listeners that might move focus
            el.addEventListener('keydown', handler, true);

            // Also attach a document-level handler that checks activeElement.id
            // This survives when Blazor re-renders and replaces the DOM node.
            if (!window.__enableTabInTextarea_docHandlers) window.__enableTabInTextarea_docHandlers = {};
            if (!window.__enableTabInTextarea_docHandlers[id]) {
                var docHandler = function (e) {
                    try {
                        var ae = document.activeElement;
                        if (ae && ae.id === id) return handler(e);
                    } catch (ex) { }
                };
                document.addEventListener('keydown', docHandler, true);
                window.__enableTabInTextarea_docHandlers[id] = docHandler;
            }

            // store handler so we could remove it later if needed
            el.__tabHandler = handler;

            // set monospace and tab-size styling
            el.style.fontFamily = 'Menlo, Monaco, Consolas, "Courier New", monospace';
            el.style.whiteSpace = 'pre-wrap';
            el.style.tabSize = '4';
            el.style.MozTabSize = '4';
            el.style.lineHeight = '1.25';
            el.__tabHandlerAttached = true;
        } catch (e) {
            console.error('enableTabInTextarea error', e);
        }
    };
})();

// Insert a tab or unindent at the cursor for a textarea by id. shift=true => unindent
window.insertTabAtCursor = function (id, shift) {
    try {
        var el = document.getElementById(id);
        if (!el) return;
        var start = el.selectionStart;
        var end = el.selectionEnd;
        var value = el.value;

        if (start !== end) {
            // operate on selected block
            var before = value.substring(0, start);
            var selected = value.substring(start, end);
            var after = value.substring(end);
            var lines = selected.split('\n');
            if (shift) {
                for (var i = 0; i < lines.length; i++) {
                    lines[i] = lines[i].replace(/^\t/, '');
                }
            } else {
                for (var i = 0; i < lines.length; i++) {
                    lines[i] = '\t' + lines[i];
                }
            }
            var newSel = lines.join('\n');
            el.value = before + newSel + after;
            el.selectionStart = start;
            el.selectionEnd = start + newSel.length;
        } else {
            if (shift) {
                // nothing to unindent on empty selection
                return;
            }
            el.value = value.substring(0, start) + '\t' + value.substring(end);
            el.selectionStart = el.selectionEnd = start + 1;
        }

        var ev;
        try { ev = new Event('input', { bubbles: true }); }
        catch (ex) { ev = document.createEvent('Event'); ev.initEvent('input', true, true); }
        el.dispatchEvent(ev);
        el.focus();
    } catch (e) { console.error('insertTabAtCursor error', e); }
};
