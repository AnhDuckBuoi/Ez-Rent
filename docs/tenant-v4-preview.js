/* Isolated visual proposal. All state is illustrative and stays in this page. */
(() => {
    'use strict';
    const $ = s => document.querySelector(s);
    const base = 'http://127.0.0.1:8770';
    const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    const paths = {
        home:'m3 11 9-8 9 8M5 10v11h14V10M9 21v-7h6v7',
        search:'M21 21l-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
        calendar:'M4 5h16v16H4zM8 3v4m8-4v4M4 10h16',
        heart:'M20 5c-3-3-6 0-8 2-2-2-5-5-8-2-4 4 0 8 8 15 8-7 12-11 8-15',
        arrow:'m9 5 7 7-7 7', back:'m15 5-7 7 7 7', down:'m6 9 6 6 6-6',
        chat:'M3 4h18v13H9l-6 4z',
        bell:'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
        check:'m5 12 4 4L19 6', clock:'M12 7v5l3 2M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
        help:'M9 8a3 3 0 1 1 5 2c-2 1-2 2-2 4m0 3v1M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
        room:'M5 3h14v18H5zM9 7h2m2 0h2M9 11h2m2 0h2M10 21v-5h4v5',
        user:'M5 21v-3a7 7 0 0 1 14 0v3M16 6a4 4 0 1 1-8 0 4 4 0 0 1 8 0'
    };
    const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${paths[name] || paths.home}"/></svg>`;
    const external = (path, text, cls = '', symbol = '') => `<a href="${base + path}" target="_blank" rel="noopener" class="${cls}">${symbol ? icon(symbol) : ''}${text}</a>`;
    const notices = [
        {id:'confirmed',title:'Lịch xem phòng đã được xác nhận',text:'Chủ nhà đã xác nhận lịch 16:00, Chủ Nhật 18/10. Xem lại thông tin trước buổi hẹn.',time:'Hôm nay, 09:20',icon:'calendar',status:'confirmed',body:'<p>Chủ nhà đã xác nhận lịch xem phòng của bạn vào <strong>16:00, Chủ Nhật ngày 18/10/2026</strong>.</p><p>Địa điểm: Ngõ 20, khu dân cư. Bạn có thể mở lịch hẹn để xem lại thông tin và nội dung trao đổi.</p>',target:'/Tenant/ViewingAppointment/Details/3'},
        {id:'pending',title:'Yêu cầu xem phòng đang chờ phản hồi',text:'Yêu cầu của bạn đã được gửi. Vui lòng chờ chủ nhà xác nhận trước khi đến.',time:'Hôm nay, 08:45',icon:'clock',status:'pending',body:'<p>Chủ nhà chưa xác nhận yêu cầu xem phòng này. Vui lòng theo dõi trạng thái trong lịch hẹn trước khi đến xem.</p><p>Bạn có thể mở lịch hẹn để xem thông tin yêu cầu và phản hồi mới nhất.</p>',target:'/Tenant/ViewingAppointment/Details/1'},
        {id:'welcome',title:'Chào mừng bạn đến với EZ Rent',text:'Tìm phòng, lưu lựa chọn và theo dõi lịch hẹn của bạn ở cùng một nơi.',time:'Hôm qua, 17:30',icon:'home',status:'welcome',body:'<p>Tìm phòng theo khu vực và nhu cầu của bạn. Khi thấy lựa chọn phù hợp, hãy lưu lại để dễ xem và so sánh.</p><p>Trang chủ giúp bạn theo dõi lịch đã xác nhận, cập nhật thông báo và đọc thêm hướng dẫn dành cho người thuê.</p>',target:'/Tenant/RentalPost'}
    ];
    const state = {page:'home',notice:null,filter:'all',articleFilter:'all',scenario:'active',read:new Set(['welcome']),checks:new Set(),open:new Set()};
    const currentNotices = () => state.scenario === 'empty' ? [] : notices;
    const unread = () => currentNotices().filter(n => !state.read.has(n.id)).length;

    function locationState() {
        const params = new URLSearchParams(location.search);
        state.page = ['home','notifications','notice'].includes(params.get('page')) ? params.get('page') : 'home';
        state.notice = notices.find(n => n.id === params.get('id'))?.id || null;
        if(state.page === 'notice' && !state.notice) state.page = 'notifications';
        if(state.page === 'notice' && state.notice) state.read.add(state.notice);
    }
    function navigate(page, notice = null) {
        state.page = page; state.notice = notice;
        const url = new URL(location.href); url.search = '';
        if(page !== 'home') url.searchParams.set('page',page);
        if(notice) url.searchParams.set('id',notice);
        history.pushState(null,'',url);
        render(); scrollTo(0,0);
        $('#main h1')?.focus({preventScroll:true});
    }
    function heading(title, text, action = '') {
        return `<div class="page-greeting"><div><h1 tabindex="-1">${title}</h1><p>${text}</p></div>${action}</div>`;
    }
    function header() {
        const home = state.page === 'home';
        $('#header-content').innerHTML = `<a href="?page=home" data-page="home" class="brand"><span class="brand-mark">${icon('home')}</span>EZ Rent</a>
            <nav class="header-links" aria-label="Điều hướng chính"><button data-page="home" class="${home?'current':''}" ${home?'aria-current="page"':''}>Trang chủ</button>${external('/Tenant/RentalPost','Tìm phòng')}${external('/Tenant/ViewingAppointment','Lịch hẹn')}${external('/Tenant/Favorite','Đã lưu')}</nav>
            <div class="header-actions"><button class="bell ${home?'':'current'}" data-page="notifications" aria-label="Thông báo, ${unread()} chưa đọc">${icon('bell')}${unread()?`<span class="unread-badge">${unread()}</span>`:''}</button><span class="header-divider"></span><span class="avatar" aria-label="Tài khoản minh họa Minh Anh">MA</span></div>`;
        $('#bottom-content').innerHTML = `<button data-page="home" class="${home?'current':''}">${icon('home')}<span>Trang chủ</span></button>${external('/Tenant/RentalPost','<span>Tìm phòng</span>','','search')}<button disabled>${icon('room')}<span>Phòng đang ở</span></button>${external('/Tenant/Dashboard/Messages','<span>Tin nhắn</span>','','chat')}<button disabled>${icon('user')}<span>Cá nhân</span></button>`;
    }
    function noticeRow(n, full = false) {
        const read = state.read.has(n.id);
        return `<button class="notice-row ${read?'read':'unread'}" data-notice="${n.id}"><span class="notice-icon ${n.status}">${icon(n.icon)}</span><span class="notice-copy"><strong>${esc(n.title)}</strong>${full?`<span>${esc(n.text)}</span>`:''}<time>${n.time}</time></span>${read?'':'<span class="unread-dot" aria-label="Chưa đọc"></span>'}</button>`;
    }
    function noticePreview() {
        return `<section class="panel notice-home" aria-labelledby="home-notices-title"><div class="section-head"><h2 id="home-notices-title">Thông báo ${unread()?`<span class="section-count">${unread()}</span>`:''}</h2><button data-page="notifications" class="text-link">Tất cả ${icon('arrow')}</button></div><div class="notice-preview">${currentNotices().slice(0,2).map(n => noticeRow(n)).join('') || '<p class="home-empty-notices">Bạn chưa có thông báo. Tin mới từ hệ thống sẽ xuất hiện tại đây.</p>'}</div></section>`;
    }
    function visit() {
        return `<section class="visit-reminder" aria-labelledby="next-visit-title"><div class="visit-label"><h2 id="next-visit-title">Lịch hẹn tiếp theo</h2>${state.scenario === 'active'?`<span class="confirmed">${icon('check')}Đã xác nhận</span>`:''}</div>${state.scenario === 'active'?`<div class="visit-content"><div class="visit-date"><strong>18</strong><span>Tháng 10</span></div><div class="visit-copy"><strong>16:00 <span>Chủ Nhật, 18/10/2026</span></strong><p>Căn hộ nhỏ, đầy đủ tiện nghi</p></div>${external('/Tenant/ViewingAppointment/Details/3','Xem lịch hẹn','button')}</div>`:`<div class="empty-copy"><p>Bạn chưa có lịch được chủ nhà xác nhận.</p>${external('/Tenant/ViewingAppointment','Theo dõi yêu cầu của bạn','text-link')}</div>`}</section>`;
    }
    function reading(id, title, body) {
        return `<details class="reading" data-article="${id}" ${state.open.has(id)?'open':''}><summary class="text-link"><span class="when-closed">${title}</span><span class="when-open">Thu gọn</span>${icon('down')}</summary><div class="reading-body">${body}</div></details>`;
    }
    function checklist() {
        const items = ['Xem lại địa chỉ và giờ hẹn đã được chủ nhà xác nhận.','Ghi sẵn câu hỏi về không gian và sinh hoạt trong phòng.','Đánh dấu những tiện ích bạn muốn xem trực tiếp.','Ghi lại thông tin chủ nhà trao đổi để đối chiếu sau buổi xem.','Dành thời gian cân nhắc trước khi quyết định thuê.'];
        return `<div class="checklist-intro"><h4>Chuẩn bị cho buổi hẹn</h4><output id="check-count" aria-live="polite">${state.checks.size}/5 mục</output></div>${items.map((text,i) => `<label class="check-row"><input type="checkbox" data-check="${i}" ${state.checks.has(i)?'checked':''}><span>${text}</span></label>`).join('')}`;
    }
    function feature() {
        return `<article class="feature-story" data-category="living"><img class="feature-photo" src="tenant-v4-assets/living.jpg" alt="Góc ghế sofa sáng cạnh cửa sổ, có cây xanh và gối tựa" width="1400" height="2100"><div class="feature-copy"><div class="story-meta"><span class="story-topic">Sống tiện nghi</span><span>Hướng dẫn EZ Rent</span></div><h3>Một góc nhỏ, thêm chút dễ chịu.</h3><p class="excerpt">Một chiếc đèn, vài cuốn sách hay một chậu cây nhỏ. Bắt đầu từ góc bạn dùng nhiều nhất để không gian sống thoải mái hơn mỗi ngày.</p>${reading('living','Đọc bài viết','<p>Hãy nghĩ đến cách bạn muốn sử dụng không gian: học tập, đọc sách hay nghỉ ngơi. Giữ những món đồ thường dùng ở gần và dành một khoảng trống cho sinh hoạt hằng ngày.</p><h4>Sắp xếp từng góc, theo cách của bạn</h4><p>Bạn có thể thử nhiều cách bố trí theo thời gian và giữ lại điều khiến mình cảm thấy thoải mái. Không cần mua tất cả mọi thứ ngay khi chuyển đến.</p>')}</div></article>`;
    }
    function supporting() {
        return `<div class="supporting-stories"><article class="small-story" data-category="visit" id="prepare-story"><img src="tenant-v4-assets/preparation.jpg" alt="Sổ tay mở và bút trên bàn gỗ" width="900" height="600" loading="lazy"><div><span class="story-topic">Trước khi xem phòng</span><h3>Đừng quên những điều bạn muốn hỏi.</h3><p>Một checklist ngắn để buổi xem phòng rõ ràng và chủ động hơn.</p>${reading('prepare','Mở checklist',checklist())}</div></article><article class="small-story" data-category="visit"><img src="tenant-v4-assets/window.jpg" alt="Cây xanh bên cửa sổ mở đón ánh sáng" width="1000" height="667" loading="lazy"><div><span class="story-topic">Khi tìm một nơi ở mới</span><h3>Lưu lại để dễ xem và so sánh.</h3><p>Giữ những lựa chọn bạn quan tâm ở cùng một nơi, rồi quay lại khi cần.</p>${reading('saved','Đọc hướng dẫn','<p>Khi tìm thấy một căn phòng phù hợp, bấm trái tim để lưu lại. Bạn có thể xem lại thông tin, so sánh với lựa chọn khác và bỏ lưu khi thay đổi ý định.</p>'+external('/Tenant/Favorite','Mở phòng đã lưu','text-link'))}</div></article></div>`;
    }
    function tools() {
        return `<section class="quick-access"><h2>Truy cập nhanh</h2><div class="quick-links">${external('/Tenant/ViewingAppointment',`<span>Lịch hẹn</span><small>${state.scenario==='active'?'1 đang chờ':'Của bạn'}</small>`,'quick-link','calendar')}${external('/Tenant/Favorite',`<span>Phòng đã lưu</span><small>${state.scenario==='active'?'2 phòng':'Chưa có'}</small>`,'quick-link','heart')}${external('/Tenant/Dashboard/Messages','<span>Tin nhắn</span>','quick-link','chat')}${external('/Tenant/Dashboard/Help','<span>Hỗ trợ</span>','quick-link','help')}</div></section>`;
    }
    function saved() {
        const list = [{id:7,title:'Căn hộ nhỏ, đầy đủ tiện nghi',address:'Bình Thạnh, Hồ Chí Minh',price:'1.800.000 đ'},{id:3,title:'Phòng có ban công, đón nắng mỗi sáng',address:'Cầu Giấy, Hà Nội',price:'3.500.000 đ'}];
        return `<section class="panel saved-panel"><div class="section-head"><h2>Đã lưu để xem lại</h2></div><div class="saved-list">${state.scenario === 'active'?list.map(r => external('/Tenant/RentalPost/Details/'+r.id,`${icon('room')}<span><strong>${r.title}</strong><small>${r.address}</small><span class="price">${r.price} <small>/ tháng</small></span></span>`,'saved-room')).join(''):'<p class="empty-copy">Bạn chưa lưu phòng nào. Bấm trái tim khi thấy lựa chọn phù hợp để xem lại tại đây.</p>'}</div>${external('/Tenant/Favorite','Xem phòng đã lưu','text-link')}</section>`;
    }
    function home() {
        const actions = `<div class="greeting-actions"><span class="greeting-date">Thứ Sáu, 16 tháng 10</span>${external('/Tenant/RentalPost','Tìm phòng','button primary','search')}</div>`;
        return heading('Chào Minh Anh!','Tin mới và những điều bạn đang quan tâm.',actions)+`
            <div class="home-grid"><div class="feed">${visit()}<section class="reading-lead"><div class="stories-head"><div class="section-head"><div><h2>Góc người thuê</h2><p>Ý tưởng nhỏ, hữu ích cho việc tìm và ở.</p></div></div><nav class="story-nav" aria-label="Chủ đề bài viết">${[['all','Dành cho bạn'],['visit','Đi xem phòng'],['living','Sống tiện nghi']].map(([id,text]) => `<button data-category-filter="${id}" aria-pressed="${state.articleFilter===id}">${text}</button>`).join('')}</nav></div>${feature()}</section>${supporting()}</div><aside class="rail" aria-label="Cập nhật và tiện ích">${noticePreview()}${tools()}${saved()}<section class="help-note"><h2>Cần trao đổi thêm?</h2><p>Xem hướng dẫn tìm phòng hoặc theo dõi yêu cầu của bạn.</p>${external('/Tenant/Dashboard/Help','Mở phần hỗ trợ','text-link')}</section></aside></div>`;
    }
    function tip() {
        return `<aside class="inbox-tip"><img src="tenant-v4-assets/preparation.jpg" alt="Sổ tay và bút dùng để ghi lại câu hỏi" width="900" height="600"><div><h2>Chuẩn bị một chút cho buổi xem phòng.</h2><p>Lưu sẵn những điều bạn muốn hỏi để không bỏ sót khi trao đổi với chủ nhà.</p><button class="text-link" data-prepare>Mở checklist ${icon('arrow')}</button></div></aside>`;
    }
    function inbox() {
        const items = currentNotices().filter(n => state.filter !== 'unread' || !state.read.has(n.id));
        const empty = state.filter === 'unread' && currentNotices().length > 0
            ? '<h2>Bạn đã đọc hết thông báo</h2><p>Các cập nhật mới sẽ xuất hiện tại đây. Bạn vẫn có thể xem lại thông báo cũ.</p><button class="button secondary" data-notice-filter="all">Xem tất cả thông báo</button>'
            : '<h2>Chưa có thông báo nào</h2><p>Khi có cập nhật từ hệ thống, bạn có thể theo dõi tại đây.</p><button class="button secondary" data-page="home">Về Trang chủ</button>';
        return heading('Thông báo của bạn','Theo dõi phản hồi và những cập nhật mới nhất.')+`<div class="inbox-grid"><section class="inbox-panel" aria-label="Danh sách thông báo"><div class="inbox-toolbar"><nav class="inbox-tabs" aria-label="Lọc thông báo"><button data-notice-filter="all" aria-pressed="${state.filter==='all'}">Tất cả <small>${currentNotices().length}</small></button><button data-notice-filter="unread" aria-pressed="${state.filter==='unread'}">Chưa đọc <small>${unread()}</small></button></nav><button class="read-all" data-read-all ${unread()?'':'disabled'}>Đánh dấu tất cả đã đọc</button></div>${items.length?`<div class="inbox-list">${items.map((n,i) => `${i===0||n.time.startsWith('Hôm qua')&&!items[i-1]?.time.startsWith('Hôm qua')?`<h2 class="day-heading">${n.time.startsWith('Hôm qua')?'Hôm qua':'Hôm nay'}</h2>`:''}${noticeRow(n,true)}`).join('')}</div>`:`<div class="inbox-empty"><span class="notice-icon">${icon('bell')}</span>${empty}</div>`}</section>${tip()}</div>`;
    }
    function detail() {
        const n = notices.find(n => n.id === state.notice);
        if(!n) return inbox();
        return `<button class="back-link" data-page="notifications">${icon('back')}Tất cả thông báo</button><article class="notice-detail"><div class="detail-status ${n.status}">${icon(n.icon)}${n.status === 'confirmed'?'Lịch đã xác nhận':n.status === 'pending'?'Chờ chủ nhà phản hồi':'Thông tin từ EZ Rent'}</div><h1 tabindex="-1">${esc(n.title)}</h1><p class="stamp">${n.time} — Đã đọc</p><div class="detail-body">${n.body}</div><div class="detail-actions">${external(n.target,n.status==='welcome'?'Tìm phòng trong ứng dụng':'Mở lịch hẹn trong ứng dụng','button primary')}<button class="button secondary" data-page="notifications">Quay lại thông báo</button></div></article>`;
    }
    const narrow = matchMedia('(max-width:899px)');
    function arrange() {
        const notice = $('.notice-home');
        if(!notice) return;
        const focus = notice.contains(document.activeElement) ? document.activeElement : null;
        if(narrow.matches) { $('.supporting-stories').before(notice); notice.classList.add('notice-home-mobile'); }
        else { $('.rail').prepend(notice); notice.classList.remove('notice-home-mobile'); }
        focus?.focus({preventScroll:true});
    }
    function filterArticles() {
        document.querySelectorAll('[data-category]').forEach(article => article.hidden = state.articleFilter !== 'all' && article.dataset.category !== state.articleFilter);
        const supporting = $('.supporting-stories');
        if(supporting) supporting.hidden = state.articleFilter === 'living';
    }
    function render() {
        header();
        $('#compare-current').href = base + (state.page === 'home' ? '/Tenant/Dashboard' : '/Tenant/Notification');
        $('#main').innerHTML = state.page === 'home' ? home() : state.page === 'notifications' ? inbox() : detail();
        document.title = (state.page === 'home'?'Trang chủ':state.page==='notifications'?'Thông báo':'Nội dung thông báo')+' · EZ Rent · Bản thử mới';
        arrange(); filterArticles();
    }
    narrow.addEventListener('change',arrange);
    document.addEventListener('click',event => {
        const button = event.target.closest('[data-page],[data-notice],[data-category-filter],[data-notice-filter],[data-read-all],[data-prepare]');
        if(!button || button.disabled) return;
        if(button.dataset.page) { event.preventDefault(); navigate(button.dataset.page); return; }
        if(button.dataset.notice) { state.read.add(button.dataset.notice); navigate('notice',button.dataset.notice); return; }
        if(button.dataset.categoryFilter) {
            state.articleFilter = button.dataset.categoryFilter;
            document.querySelectorAll('[data-category-filter]').forEach(b => b.setAttribute('aria-pressed',String(b === button)));
            filterArticles(); return;
        }
        if(button.dataset.noticeFilter) { state.filter = button.dataset.noticeFilter; render(); document.querySelector(`.inbox-tabs [data-notice-filter="${state.filter}"]`)?.focus(); return; }
        if(button.hasAttribute('data-read-all')) { currentNotices().forEach(n => state.read.add(n.id)); render(); $('.inbox-tabs button')?.focus(); $('#live-status').textContent = 'Đã đánh dấu tất cả thông báo là đã đọc.'; return; }
        if(button.hasAttribute('data-prepare')) {
            state.articleFilter = 'all'; state.open.add('prepare'); navigate('home');
            const target = $('#prepare-story summary'); target.focus(); target.scrollIntoView({block:'center'});
        }
    });
    document.addEventListener('toggle',event => {
        const details = event.target;
        if(details.matches('[data-article]')) details.open ? state.open.add(details.dataset.article) : state.open.delete(details.dataset.article);
    },true);
    document.addEventListener('change',event => {
        if(event.target.matches('[data-check]')) {
            const id = Number(event.target.dataset.check);
            event.target.checked ? state.checks.add(id) : state.checks.delete(id);
            $('#check-count').textContent = `${state.checks.size}/5 mục`;
        }
    });
    $('#scenario').addEventListener('change',event => { state.scenario = event.target.value; if(state.page==='notice') state.page='notifications'; render(); });
    window.addEventListener('popstate',() => { locationState(); render(); });
    locationState(); render();
})();
