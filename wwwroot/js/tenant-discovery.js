(() => {
    'use strict';
    const results = document.getElementById('rental-results');
    const form = document.getElementById('rental-search');
    const errorBox = document.getElementById('search-error');
    let requestNumber = 0;
    let controller;
    let favoriteRevision = 0;
    const confirmedFavorites = new Map();
    const pendingFavorites = new Set();
    const rangeNames = ['MinPrice', 'MaxPrice', 'MinArea', 'MaxArea'];

    function validateRanges() {
        if (!form) return true;
        const messages = [];
        for (const [minName, maxName, label] of [['MinPrice', 'MaxPrice', 'Giá'], ['MinArea', 'MaxArea', 'Diện tích']]) {
            const min = form.elements.namedItem(minName);
            const max = form.elements.namedItem(maxName);
            for (const field of [min, max]) {
                field.setCustomValidity('');
                field.removeAttribute('aria-invalid');
                field.removeAttribute('aria-describedby');
                let message = '';
                if (field.validity.badInput || (field.value !== '' && !Number.isFinite(field.valueAsNumber)))
                    message = `${label} phải là một số hợp lệ.`;
                else if (field.value !== '' && field.valueAsNumber < 0) message = `${label} không được âm.`;
                else if (field.max && field.value !== '' && field.valueAsNumber > Number(field.max))
                    message = `${label} vượt giới hạn cho phép.`;
                if (message) { field.setCustomValidity(message); messages.push(message); }
            }
            if (min.validity.valid && max.validity.valid && min.value !== '' && max.value !== '' && min.valueAsNumber > max.valueAsNumber) {
                const message = `${label} tối thiểu phải nhỏ hơn hoặc bằng ${label.toLowerCase()} tối đa.`;
                max.setCustomValidity(message);
                messages.push(message);
            }
            for (const field of [min, max]) if (!field.validity.valid) {
                field.setAttribute('aria-invalid', 'true'); field.setAttribute('aria-describedby', 'range-error');
            }
        }
        const error = document.getElementById('range-error');
        if (error) { error.textContent = [...new Set(messages)].join(' '); error.hidden = messages.length === 0; }
        return messages.length === 0;
    }

    function favoriteForms(postId) {
        return [...document.querySelectorAll('[data-favorite-form]')].filter(other =>
            other.querySelector('[name="RentalPostId"]').value === String(postId));
    }
    function applyFavoriteState(postId, saved) {
        for (const other of favoriteForms(postId)) {
            other.querySelector('[name="Saved"]').value = String(!saved);
            const button = other.querySelector('button');
            button.setAttribute('aria-pressed', String(saved));
            button.classList.toggle('is-saved', saved);
            const title = other.closest('.rental-card')?.querySelector('h3')?.textContent || document.querySelector('.detail-heading h1')?.textContent || 'phòng';
            button.setAttribute('aria-label', `${saved ? 'Bỏ lưu' : 'Lưu'} ${title.trim()}`);
            other.querySelector('[data-heart]').textContent = saved ? '♥' : '♡';
            const label = other.querySelector('[data-save-label]');
            if (label) label.textContent = saved ? 'Đã lưu phòng' : 'Lưu phòng yêu thích';
        }
    }
    function setFavoriteBusy(postId, busy) {
        for (const other of favoriteForms(postId)) {
            const button = other.querySelector('button');
            button.disabled = busy;
            if (busy) other.setAttribute('aria-busy', 'true'); else other.removeAttribute('aria-busy');
            const label = other.querySelector('[data-save-label]');
            if (label) label.textContent = busy ? 'Đang cập nhật…' : button.getAttribute('aria-pressed') === 'true' ? 'Đã lưu phòng' : 'Lưu phòng yêu thích';
        }
    }
    function toast(message, error = false) {
        window.TenantFeedback?.show(message, error);
    }
    function syncForm(url) {
        if (!form) return;
        const params = new URL(url, location.origin).searchParams;
        const district = document.getElementById('filter-district');
        const options = document.getElementById('district-options');
        if (district && options) district.replaceChildren(options.content.cloneNode(true));
        for (const element of form.elements) {
            if (element.name && element.type !== 'submit') element.value = params.get(element.name) ?? (element.name === 'Sort' ? 'newest' : '');
        }
        const count = document.querySelector('[data-filter-count-value]')?.dataset.filterCountValue || '0';
        const badge = document.querySelector('[data-filter-count]');
        if (badge) badge.textContent = count !== '0' ? `(${count})` : '';
        if ([...form.elements].some(e => e.name !== 'Keyword' && e.name !== 'Sort' && e.value && e.name))
            form.querySelector('.filter-panel').open = true;
        validateRanges();
    }
    async function loadResults(url, push = true, scroll = false) {
        if (!results) return;
        const number = ++requestNumber;
        const revisionAtStart = favoriteRevision;
        controller?.abort();
        controller = new AbortController();
        results.setAttribute('aria-busy', 'true');
        if (errorBox) errorBox.hidden = true;
        try {
            const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: controller.signal });
            if (!response.ok || response.redirected) throw new Error('Không tải được kết quả. Vui lòng thử lại hoặc tải lại trang để đăng nhập.');
            const html = await response.text();
            if (number !== requestNumber) return;
            const fragment = document.createElement('template');
            fragment.innerHTML = html;
            const marker = fragment.content.querySelector('[data-current-url]');
            if (!marker) throw new Error('Không tải được kết quả. Vui lòng thử lại.');
            const invalid = fragment.content.querySelector('.validation-summary-errors');
            const canonical = invalid ? url : marker.dataset.currentUrl;
            results.replaceChildren(fragment.content);
            // A listing response may have been read before a save completed. Keep the confirmed change.
            for (const [id, change] of confirmedFavorites) if (change.revision > revisionAtStart)
                applyFavoriteState(id, change.saved);
            for (const id of pendingFavorites) setFavoriteBusy(id, true);
            if (push) history.pushState(null, '', canonical); else history.replaceState(null, '', canonical);
            syncForm(canonical);
            if (scroll) results.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } catch (error) {
            if (error.name === 'AbortError' || number !== requestNumber) return;
            if (errorBox) { errorBox.textContent = error instanceof TypeError ? 'Kết nối bị gián đoạn. Vui lòng thử lại.' : error.message || 'Không tải được kết quả. Vui lòng thử lại.'; errorBox.hidden = false; }
        } finally {
            if (number === requestNumber) results.removeAttribute('aria-busy');
        }
    }
    form?.addEventListener('submit', event => {
        event.preventDefault();
        if (!validateRanges()) { form.reportValidity(); return; }
        const url = new URL(form.action, location.origin);
        for (const [key, value] of new FormData(form)) if (value !== '') url.searchParams.set(key, value);
        loadResults(url.pathname + url.search);
    });
    form?.addEventListener('input', event => { if (rangeNames.includes(event.target.name)) validateRanges(); });
    validateRanges();
    document.getElementById('filter-city')?.addEventListener('change', () => {
        const district = document.getElementById('filter-district');
        if (district) district.value = '';
        form.requestSubmit();
    });
    document.addEventListener('click', event => {
        const link = event.target.closest('[data-page-link], [data-filter-link]');
        if (link && results && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0) {
            event.preventDefault();
            loadResults(link.href, true, link.hasAttribute('data-page-link'));
        }
        const thumb = event.target.closest('[data-gallery-src]');
        if (thumb) {
            const main = document.getElementById('main-room-image');
            main.dataset.fallback = '/images/room-placeholder.svg';
            main.src = thumb.dataset.gallerySrc;
            document.querySelectorAll('[data-gallery-src]').forEach(button => button.setAttribute('aria-pressed', String(button === thumb)));
        }
    });
    window.addEventListener('popstate', () => loadResults(location.pathname + location.search, false));
    document.addEventListener('submit', async event => {
        const favoriteForm = event.target.closest('[data-favorite-form]');
        if (!favoriteForm) return;
        event.preventDefault();
        const button = favoriteForm.querySelector('button');
        const postId = favoriteForm.querySelector('[name="RentalPostId"]').value;
        if (button.disabled || pendingFavorites.has(postId)) return;
        const body = new FormData(favoriteForm);
        pendingFavorites.add(postId);
        setFavoriteBusy(postId, true);
        try {
            const response = await fetch(favoriteForm.action, { method: 'POST', body, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const payload = response.headers.get('Content-Type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok || response.redirected || !payload || typeof payload.saved !== 'boolean')
                throw new Error(payload?.message || 'Chưa lưu được thay đổi. Vui lòng thử lại hoặc tải lại trang để đăng nhập.');
            confirmedFavorites.set(postId, { saved: payload.saved, revision: ++favoriteRevision });
            applyFavoriteState(postId, payload.saved);
            toast(payload.message);
            if (document.querySelector('[data-favorites-list]')) await loadResults(location.pathname + location.search, false);
        } catch (error) {
            toast(error instanceof TypeError ? 'Kết nối bị gián đoạn. Vui lòng thử lại.' : error.message || 'Chưa lưu được thay đổi. Vui lòng thử lại.', true);
        } finally {
            pendingFavorites.delete(postId);
            setFavoriteBusy(postId, false);
        }
    });
    document.addEventListener('error', event => {
        const image = event.target;
        if (image instanceof HTMLImageElement && image.dataset.fallback) {
            const fallback = image.dataset.fallback;
            delete image.dataset.fallback;
            image.src = fallback;
        }
    }, true);
})();
