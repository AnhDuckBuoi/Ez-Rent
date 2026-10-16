(() => {
    'use strict';
    const layout = document.querySelector('[data-home-layout]');
    if (layout) {
        const shortcuts = layout.querySelector('.home-shortcuts');
        const news = layout.querySelector('.home-news');
        const aside = layout.querySelector('.home-aside');
        const narrow = matchMedia('(max-width:999px)');
        function arrange() {
            const active = shortcuts.contains(document.activeElement) ? document.activeElement : null;
            if (narrow.matches) news.before(shortcuts);
            else aside.prepend(shortcuts);
            active?.focus({ preventScroll: true });
        }
        narrow.addEventListener('change', arrange);
        arrange();
    }

    const filters = document.querySelector('[data-guide-filters]');
    if (filters) {
        filters.hidden = false;
        filters.addEventListener('click', event => {
            const button = event.target.closest('[data-guide-filter]');
            if (!button) return;
            const selected = button.dataset.guideFilter;
            filters.querySelectorAll('button').forEach(b => b.setAttribute('aria-pressed', String(b === button)));
            document.querySelectorAll('[data-guide-category]').forEach(article => {
                article.hidden = selected !== 'all' && article.dataset.guideCategory !== selected;
            });
            document.querySelector('.home-stories').classList.toggle('filtered', selected !== 'all');
        });
    }
    const checks = [...document.querySelectorAll('[data-home-check]')];
    function updateChecklist() {
        checks.forEach(input => input.closest('label').classList.toggle('done', input.checked));
        const count = document.getElementById('checklist-count');
        if (count) count.textContent = `${checks.filter(input => input.checked).length}/${checks.length} mục`;
    }
    checks.forEach(input => input.addEventListener('change', updateChecklist));
    window.addEventListener('pageshow', updateChecklist);
    updateChecklist();
})();
