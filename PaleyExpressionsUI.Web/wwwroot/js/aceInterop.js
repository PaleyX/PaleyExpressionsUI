window.aceInterop = (function () {
    console.log('aceInterop: script loaded');
    const editors = {};

    return {
        create: function (id, initialValue) {
            console.log(`aceInterop.create called for id=${id}`);
            try {
                const el = document.getElementById(id);
                if (!el) {
                    console.warn(`aceInterop.create: element not found: ${id}`);
                    return;
                }
                // make container focusable so editor can receive keyboard events
                try { el.tabIndex = 0; } catch { }

                // Poll for window.ace being available (race between script load and Blazor render)
                const maxAttempts = 40; // ~2s with 50ms interval
                const intervalMs = 50;

                const attemptCreate = function (attemptsLeft) {
                    if (window.ace) {
                        try {
                            console.log(`aceInterop: ace available, initializing editor for ${id}`);
                            // If editor already exists, ensure it's still attached to the current DOM.
                            if (editors[id]) {
                                try {
                                    var existing = editors[id];
                                    // If the existing editor's container is no longer in the document, destroy it
                                    if (!document.contains(existing.container)) {
                                        try { existing.destroy && existing.destroy(); } catch (ex) { }
                                        try { delete editors[id]; } catch (ex) { }
                                    }
                                    else {
                                        try { existing.setValue(initialValue || "", -1); existing.clearSelection(); } catch (e) { console.error('aceInterop.update error', e); }
                                        return;
                                    }
                                }
                                catch (e) { console.error('aceInterop.checkExisting error', e); }
                            }

                            const editor = window.ace.edit(id);
                            // Use a light theme similar to Visual Studio
                            editor.setTheme("ace/theme/chrome");
                            editor.session.setMode("ace/mode/csharp");
                            // Disable the active-line row highlight (can be very dark in some themes)
                            try { editor.setHighlightActiveLine(false); } catch { }
                            try { editor.setOption && editor.setOption('highlightActiveLine', false); } catch { }
                            editor.setValue(initialValue || "", -1);
                            editor.clearSelection();
                            editor.setOptions({ fontSize: "12pt", wrap: true, behavioursEnabled: true });
                            try { editor.setReadOnly(false); } catch { }
                            // make sure clicking the container focuses the editor
                            el.addEventListener('click', function () { try { editor.focus(); } catch { } });
                            editors[id] = editor;
                            console.log(`aceInterop: editor initialized for ${id}`);
                        }
                        catch (e) {
                            console.error('aceInterop.create error', e);
                        }
                    }
                    else if (attemptsLeft > 0) {
                        if (attemptsLeft % 10 === 0) console.log(`aceInterop: waiting for ace.js, attemptsLeft=${attemptsLeft}`);
                        setTimeout(function () { attemptCreate(attemptsLeft - 1); }, intervalMs);
                    }
                    else {
                        // final fallback: set element text so user sees something
                        try { el.textContent = initialValue || ""; } catch { }
                        console.warn('aceInterop: ace.js not available; editor not initialized');
                    }
                };

                attemptCreate(maxAttempts);
            }
            catch (e) {
                console.error('aceInterop.create error', e);
            }
        },
        getValue: function (id) {
            try {
                const e = editors[id];
                const val = e ? e.getValue() : null;
                console.log(`aceInterop.getValue id=${id} -> ${val ? '[value length=' + val.length + ']' : 'null'}`);
                return val;
            } catch (ex) {
                console.error('aceInterop.getValue error', ex);
                return null;
            }
        },
        setValue: function (id, val) {
            try {
                const e = editors[id];
                if (e) {
                    e.setValue(val || "", -1);
                    e.clearSelection();
                    console.log(`aceInterop.setValue id=${id} set ${val ? '[value length=' + val.length + ']' : 'empty'}`);
                }
                else {
                    console.warn(`aceInterop.setValue: editor not found for id=${id}`);
                }
            } catch (ex) { console.error('aceInterop.setValue error', ex); }
        },
        focus: function (id) {
            try {
                const e = editors[id];
                if (e) { e.focus(); console.log(`aceInterop.focus id=${id}`); }
                else console.warn(`aceInterop.focus: editor not found for id=${id}`);
            } catch (ex) { console.error('aceInterop.focus error', ex); }
        },
        resize: function (id) {
            try {
                const e = editors[id];
                if (e) { e.resize(); console.log(`aceInterop.resize id=${id}`); }
                else console.warn(`aceInterop.resize: editor not found for id=${id}`);
            } catch (ex) { console.error('aceInterop.resize error', ex); }
        },
        dispose: function (id) {
            try {
                const e = editors[id];
                if (e) {
                    e.destroy();
                    delete editors[id];
                    console.log(`aceInterop.dispose id=${id}`);
                }
                else console.warn(`aceInterop.dispose: editor not found for id=${id}`);
            } catch (ex) { console.error('aceInterop.dispose error', ex); }
        }
    };
})();

// Try a final initialization attempt after window load to help with timing issues
window.addEventListener && window.addEventListener('load', function () {
    try {
        console.log('aceInterop: window.load fired');
    } catch { }
});
