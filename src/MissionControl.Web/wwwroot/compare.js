// Séparateur réglable entre les colonnes .NET 10 et .NET 11.
// Délégation d'événements : fonctionne quels que soient les rendus Blazor. La largeur vit dans
// la variable CSS --left de .compare, que Blazor ne touche pas (aucun attribut style rendu).
if (!window.__missionSplitter) {
    window.__missionSplitter = true;

    const MIN = 15, MAX = 85;
    const clamp = pct => Math.min(MAX, Math.max(MIN, pct));
    const current = grid => parseFloat(grid.style.getPropertyValue('--left')) || 50;

    document.addEventListener('pointerdown', e => {
        const splitter = e.target.closest('.splitter');
        if (!splitter || e.button !== 0) return;
        e.preventDefault();
        const grid = splitter.parentElement;
        const rect = grid.getBoundingClientRect();
        splitter.setPointerCapture(e.pointerId);
        grid.classList.add('dragging');

        const move = ev => grid.style.setProperty('--left', clamp((ev.clientX - rect.left) / rect.width * 100) + '%');
        const end = () => {
            grid.classList.remove('dragging');
            splitter.removeEventListener('pointermove', move);
            splitter.removeEventListener('pointerup', end);
            splitter.removeEventListener('pointercancel', end);
        };
        splitter.addEventListener('pointermove', move);
        splitter.addEventListener('pointerup', end);
        splitter.addEventListener('pointercancel', end);
    });

    document.addEventListener('dblclick', e => {
        const splitter = e.target.closest('.splitter');
        if (splitter) splitter.parentElement.style.setProperty('--left', '50%');
    });

    // Au clavier : flèches par pas de 5 %, Origine pour revenir à 50/50.
    document.addEventListener('keydown', e => {
        const splitter = e.target.closest && e.target.closest('.splitter');
        if (!splitter) return;
        const grid = splitter.parentElement;
        const step = { ArrowLeft: -5, ArrowRight: 5 }[e.key];
        if (step) grid.style.setProperty('--left', clamp(current(grid) + step) + '%');
        else if (e.key === 'Home') grid.style.setProperty('--left', '50%');
        else return;
        e.preventDefault();
        e.stopPropagation();
    }, true);
}
