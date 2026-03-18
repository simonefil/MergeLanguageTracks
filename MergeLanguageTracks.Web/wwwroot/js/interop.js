// Keyboard capture - prevents browser defaults and forwards to .NET
export function captureKeyboard(dotNetRef) {
    document.addEventListener('keydown', function (e) {
        var key = e.key;
        var ctrl = e.ctrlKey;
        var shift = e.shiftKey;
        var alt = e.altKey;
        var tagName = document.activeElement ? document.activeElement.tagName : '';

        // Se un input/textarea ha focus, permetti digitazione normale
        if (tagName === 'INPUT' || tagName === 'TEXTAREA') {
            // Tab nell'input path-bar: previeni cambio focus (autocomplete gestito da Blazor)
            if (key === 'Tab' && document.activeElement.classList.contains('path-bar-input')) {
                e.preventDefault();
            }
            // Escape chiude dialogs
            if (key === 'Escape') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnKeyDown', key, ctrl, shift, alt);
            }
            return;
        }

        // Previeni default browser per F-keys
        if (key === 'F1' || key === 'F2' || key === 'F5' || key === 'F6' ||
            key === 'F7' || key === 'F8' || key === 'F9' || key === 'F10') {
            e.preventDefault();
        }

        // Previeni Ctrl+P (print), Ctrl+N (new window)
        if (ctrl && (key === 'p' || key === 'n')) {
            e.preventDefault();
        }

        // Invia a .NET
        dotNetRef.invokeMethodAsync('OnKeyDown', key, ctrl, shift, alt);
    });
}

// Tema
export function setTheme(themeName) {
    document.documentElement.setAttribute('data-webtui-theme', themeName);
    localStorage.setItem('mlt-theme', themeName);
}

export function loadSavedTheme() {
    var saved = localStorage.getItem('mlt-theme');
    if (saved) {
        document.documentElement.setAttribute('data-webtui-theme', saved);
    }
    return saved || 'nord';
}

// Scroll log alla fine
export function scrollLogToBottom() {
    var el = document.querySelector('.log-panel .mlt-panel-body');
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
}

// Scroll riga selezionata in vista
export function scrollSelectedRowIntoView() {
    var row = document.querySelector('.episode-table tbody tr.selected');
    if (row) {
        row.scrollIntoView({ block: 'nearest' });
    }
}
