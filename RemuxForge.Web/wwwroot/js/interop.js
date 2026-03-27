// Keyboard capture - filtra tasti e inoltra a .NET
export function captureKeyboard(dotNetRef) {
    // Rimuovi handler precedente se presente
    if (window._rfKeyHandler) {
        document.removeEventListener('keydown', window._rfKeyHandler);
    }

    window._rfKeyHandler = function (e) {
        var key = e.key;
        var ctrl = e.ctrlKey;
        var shift = e.shiftKey;
        var alt = e.altKey;
        var tagName = document.activeElement ? document.activeElement.tagName : '';

        // Se un input/textarea ha focus, gestisci solo Tab e Escape
        if (tagName === 'INPUT' || tagName === 'TEXTAREA') {
            if (key === 'Tab' && document.activeElement.classList.contains('path-bar-input')) {
                e.preventDefault();
            }
            if (key === 'Escape') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnKeyDown', key, ctrl, shift, alt);
            }
            return;
        }

        // Filtra: invia solo tasti funzione, Escape, Enter, Delete e combinazioni Ctrl
        var isFKey = key.startsWith('F') && key.length <= 3 && !isNaN(key.substring(1));
        var isSpecial = key === 'Escape' || key === 'Enter' || key === 'Delete';
        if (!isFKey && !isSpecial && !ctrl) {
            return;
        }

        // Previeni default browser per F-keys
        if (isFKey) {
            e.preventDefault();
        }

        // Previeni Ctrl+P (print), Ctrl+N (new window)
        if (ctrl && (key === 'p' || key === 'n')) {
            e.preventDefault();
        }

        // Invia a .NET
        dotNetRef.invokeMethodAsync('OnKeyDown', key, ctrl, shift, alt);
    };

    document.addEventListener('keydown', window._rfKeyHandler);
}

// Rimuovi handler tastiera
export function releaseKeyboard() {
    if (window._rfKeyHandler) {
        document.removeEventListener('keydown', window._rfKeyHandler);
        window._rfKeyHandler = null;
    }
}

// Tema
export function setTheme(themeName) {
    document.documentElement.setAttribute('data-webtui-theme', themeName);
    localStorage.setItem('rf-theme', themeName);
}

export function loadSavedTheme() {
    var saved = localStorage.getItem('rf-theme');
    if (saved) {
        document.documentElement.setAttribute('data-webtui-theme', saved);
    }
    return saved || 'nord';
}

// Copia testo nella clipboard
export function copyToClipboard(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text);
    }
}

// Scroll log alla fine
export function scrollLogToBottom() {
    var el = document.querySelector('.log-panel .rf-panel-body');
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
}
