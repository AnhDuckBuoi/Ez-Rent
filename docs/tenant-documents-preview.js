/* Prototype only: no fetch, upload, localStorage, database write, or signing endpoint. */
(() => {
    'use strict';

    const form = document.getElementById('contract-form');
    const money = new Intl.NumberFormat('vi-VN');
    const clock = new Intl.DateTimeFormat('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short'
    });
    const fileVersions = [];
    const objectUrls = new Set();
    let draftVersion = 1;
    let currentFile = null;

    function showError(element, message) {
        element.textContent = message;
        element.hidden = !message;
    }

    function displayDate(value) {
        const [year, month, day] = value.split('-');
        return `${day}/${month}/${year}`;
    }

    // Copy values into a snapshot. Subsequent form edits cannot mutate that snapshot.
    function createSnapshot() {
        const data = Object.fromEntries(new FormData(form));
        return Object.freeze(Object.fromEntries(Object.entries(data).map(([key, value]) => [key, value.trim()])));
    }

    function renderSnapshot(snapshot) {
        document.querySelectorAll('[data-value]').forEach(element => {
            const key = element.dataset.value;
            let value = snapshot[key];
            if (key === 'startDate' || key === 'endDate') value = displayDate(value);
            if (key === 'rent' || key === 'deposit') value = `${money.format(Number(value))} đ`;
            element.textContent = value || (key === 'terms'
                ? 'Chưa bổ sung. Hai bên cần thống nhất và hoàn thiện nội dung trước khi ký.'
                : 'Chưa bổ sung');
        });
        document.getElementById('version-label').textContent = `Bản nháp ${draftVersion} · Chưa ký`;
    }

    form.addEventListener('input', () => {
        document.getElementById('draft-state').textContent = 'Có thay đổi chưa đưa vào mẫu. Nhấn “Tạo bản xem trước” để cập nhật.';
        showError(document.getElementById('form-error'), '');
        document.getElementById('end-date').setCustomValidity('');
    });

    form.addEventListener('submit', event => {
        event.preventDefault();
        const snapshot = createSnapshot();
        const error = document.getElementById('form-error');
        if (!snapshot.tenantName || !snapshot.landlordName) {
            showError(error, 'Hãy nhập họ tên người thuê và chủ nhà.');
            document.getElementById(!snapshot.tenantName ? 'tenant-name' : 'landlord-name').focus();
            return;
        }
        if (snapshot.endDate < snapshot.startDate) {
            const endDate = document.getElementById('end-date');
            showError(error, 'Ngày kết thúc phải bằng hoặc sau ngày bắt đầu thuê.');
            endDate.setCustomValidity('Ngày kết thúc phải bằng hoặc sau ngày bắt đầu thuê.');
            endDate.focus();
            return;
        }
        draftVersion += 1;
        renderSnapshot(snapshot);
        showError(error, '');
        document.getElementById('draft-state').textContent = `Đã cập nhật bản nháp ${draftVersion} trong trình duyệt. Bản này chưa được lưu trên hệ thống.`;
        // Keep the primary task usable on narrow screens without a second long scroll.
        document.getElementById('document-title').setAttribute('tabindex', '-1');
        document.getElementById('document-title').focus();
    });

    document.getElementById('print-contract').addEventListener('click', () => window.print());

    function selectSection() {
        const key = location.hash === '#invoice' ? 'invoice' : 'contract';
        ['contract', 'invoice'].forEach(id => { document.getElementById(id).hidden = id !== key; });
        document.querySelectorAll('.section-tabs [data-section]').forEach(link => {
            if (link.dataset.section === key) link.setAttribute('aria-current', 'page');
            else link.removeAttribute('aria-current');
        });
    }
    window.addEventListener('hashchange', selectSection);

    function makeObjectUrl(file) {
        const url = URL.createObjectURL(file);
        objectUrls.add(url);
        return url;
    }

    // These checks are only for a local preview, not the future server upload policy.
    async function identifyFile(file, allowPdf, maxBytes) {
        if (!file.size || file.size > maxBytes) throw new Error(`Tệp phải có dung lượng từ 1 byte đến ${maxBytes / 1024 / 1024} MB.`);
        const bytes = new Uint8Array(await file.slice(0, 12).arrayBuffer());
        const isPdf = bytes[0] === 0x25 && bytes[1] === 0x50 && bytes[2] === 0x44 && bytes[3] === 0x46 && bytes[4] === 0x2d;
        const isJpeg = bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff;
        const isPng = [137, 80, 78, 71, 13, 10, 26, 10].every((value, index) => bytes[index] === value);
        if (allowPdf && isPdf && /\.pdf$/i.test(file.name)) return 'pdf';
        if ((isJpeg && /\.jpe?g$/i.test(file.name)) || (isPng && /\.png$/i.test(file.name))) return 'image';
        throw new Error(allowPdf ? 'Hãy chọn đúng tệp PDF, JPG hoặc PNG.' : 'Hãy chọn đúng ảnh JPG hoặc PNG.');
    }

    async function ensureReadableImage(url) {
        const image = new Image();
        image.src = url;
        await image.decode();
        if (image.naturalWidth > 8192 || image.naturalHeight > 8192 || image.naturalWidth * image.naturalHeight > 40000000) {
            throw new Error('Ảnh quá lớn. Hãy chọn ảnh không quá 8.192 px mỗi chiều và 40 triệu điểm ảnh.');
        }
    }

    function openDocument(version) {
        const viewer = document.getElementById('contract-viewer');
        viewer.replaceChildren();
        const content = document.createElement(version.kind === 'pdf' ? 'iframe' : 'img');
        content.src = version.url;
        if (version.kind === 'pdf') {
            content.title = `Tài liệu: ${version.file.name}`;
            content.setAttribute('sandbox', '');
        } else content.alt = `Bản hợp đồng được chọn: ${version.file.name}`;
        viewer.append(content);
        document.getElementById('contract-dialog-title').textContent = `Tài liệu · Phiên bản ${version.number}`;
        const download = document.getElementById('download-original');
        download.href = version.url;
        download.download = version.file.name;
        document.getElementById('contract-dialog').showModal();
    }

    function renderFileHistory() {
        const history = document.getElementById('file-history');
        history.replaceChildren();
        [...fileVersions].reverse().forEach(version => {
            const item = document.createElement('li');
            const name = document.createElement('strong');
            name.textContent = `Phiên bản ${version.number} · ${version.file.name}`;
            const meta = document.createElement('small');
            meta.textContent = `${clock.format(version.selectedAt)} · ${version.confirmed ? 'Đã đối chiếu trong phiên thử' : 'Chưa đối chiếu'}`;
            const view = document.createElement('button');
            view.type = 'button';
            view.className = 'button secondary';
            view.textContent = `Xem phiên bản ${version.number}`;
            view.addEventListener('click', () => openDocument(version));
            item.append(name, meta, view);
            history.append(item);
        });
    }

    document.getElementById('contract-file').addEventListener('change', async event => {
        const input = event.currentTarget;
        const file = input.files[0];
        if (!file) return;
        const error = document.getElementById('contract-file-error');
        input.disabled = true;
        let url;
        try {
            if (fileVersions.length >= 10) throw new Error('Phiên thử giữ tối đa 10 tệp. Hãy tải lại trang để bắt đầu phiên mới.');
            const kind = await identifyFile(file, true, 10 * 1024 * 1024);
            url = makeObjectUrl(file);
            if (kind === 'image') await ensureReadableImage(url);
            currentFile = { number: fileVersions.length + 1, file, kind, url, selectedAt: new Date(), confirmed: false };
            fileVersions.push(currentFile);
            document.getElementById('archive-empty').hidden = true;
            document.getElementById('archive-detail').hidden = false;
            document.getElementById('file-name').textContent = file.name;
            document.getElementById('file-meta').textContent = `Phiên bản ${currentFile.number} · ${(file.size / 1024 / 1024).toFixed(2)} MB · Chỉ có trong trình duyệt`;
            document.getElementById('confirm-document').checked = false;
            document.getElementById('confirmation-state').textContent = 'Tài liệu mới cần đối chiếu lại. Chưa xác nhận trong phiên thử.';
            renderFileHistory();
            showError(error, '');
        } catch (failure) {
            if (url) { URL.revokeObjectURL(url); objectUrls.delete(url); }
            showError(error, failure.message || 'Chưa đọc được tệp. Hãy thử tệp khác.');
        } finally {
            input.disabled = false;
            input.value = '';
        }
    });

    document.getElementById('open-contract').addEventListener('click', () => { if (currentFile) openDocument(currentFile); });
    document.getElementById('confirm-document').addEventListener('change', event => {
        if (!currentFile) return;
        currentFile.confirmed = event.currentTarget.checked;
        document.getElementById('confirmation-state').textContent = currentFile.confirmed
            ? `Đã ghi nhận đối chiếu trong phiên thử lúc ${clock.format(new Date())}. Chưa lưu lên hệ thống.`
            : 'Chưa xác nhận đối chiếu trong phiên thử.';
        renderFileHistory();
    });

    document.querySelectorAll('[data-meter]').forEach(entry => {
        const input = entry.querySelector('[data-image-input]');
        const photo = entry.querySelector('[data-photo]');
        const image = photo.querySelector('img');
        let currentUrl;
        input.addEventListener('change', async () => {
            const file = input.files[0];
            if (!file) return;
            const error = entry.querySelector('[data-image-error]');
            input.disabled = true;
            let nextUrl;
            try {
                await identifyFile(file, false, 5 * 1024 * 1024);
                nextUrl = makeObjectUrl(file);
                await ensureReadableImage(nextUrl);
                image.src = nextUrl;
                if (currentUrl) { URL.revokeObjectURL(currentUrl); objectUrls.delete(currentUrl); }
                currentUrl = nextUrl;
                photo.hidden = false;
                entry.querySelector('[data-empty]').hidden = true;
                showError(error, '');
            } catch (failure) {
                if (nextUrl) { URL.revokeObjectURL(nextUrl); objectUrls.delete(nextUrl); }
                showError(error, failure.message || 'Chưa đọc được ảnh. Hãy thử ảnh khác.');
            } finally {
                input.disabled = false;
                input.value = '';
            }
        });
        photo.addEventListener('click', () => {
            const large = document.getElementById('large-image');
            large.src = currentUrl;
            large.alt = image.alt;
            document.getElementById('image-dialog-title').textContent = entry.dataset.meter === 'electric' ? 'Ảnh công tơ điện' : 'Ảnh đồng hồ nước';
            document.getElementById('image-dialog').showModal();
        });
    });

    document.querySelectorAll('[data-close-dialog]').forEach(button => {
        button.addEventListener('click', () => button.closest('dialog').close());
    });
    document.getElementById('contract-dialog').addEventListener('close', () => document.getElementById('contract-viewer').replaceChildren());
    window.addEventListener('pagehide', event => {
        if (!event.persisted) objectUrls.forEach(url => URL.revokeObjectURL(url));
    });
    renderSnapshot(createSnapshot());
    selectSection();
})();
