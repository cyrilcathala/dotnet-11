// Navigation au clavier pour la scène (télécommande de présentation : flèches / Page suivante).
if (!window.__missionKeys) {
    window.__missionKeys = true;
    document.addEventListener('keydown', e => {
        if (e.target.closest('input, textarea, select, [contenteditable], .splitter')) return;
        const next = ['ArrowRight', 'PageDown'].includes(e.key);
        const prev = ['ArrowLeft', 'PageUp'].includes(e.key);
        if (!next && !prev) return;
        const link = document.querySelector(next ? '[data-nav="next"]' : '[data-nav="prev"]');
        if (link) { e.preventDefault(); link.click(); }
    });
}
