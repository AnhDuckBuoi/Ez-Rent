/* Home design preview. All notices/articles are local examples, never server data. */
'use strict';
window.createTenantHomeOutline = ({state,rooms,icon,roomArt,esc,time,date,money,go,render}) => {
    const seen = new Set(['welcome']);
    const mobileLayout = matchMedia('(max-width:999px)');
    const checked = new Set();
    let expandedArticle = null;
    mobileLayout.addEventListener('change',()=>{if(state.view==='home')render();});
    let category = 'all';
    const $ = selector => document.querySelector(selector);
    let dialogOpener=null;
    $('#home-dialog').addEventListener('close',()=>{if(dialogOpener&&!dialogOpener.isConnected)$('.header-bell').focus();});
    const articles = [
        {id:1,category:'visit',label:'Trước khi xem phòng',title:'Một buổi xem phòng, bắt đầu từ sự chuẩn bị.',excerpt:'Ghi lại những điều bạn muốn hỏi để buổi xem phòng thoải mái và rõ ràng hơn.',theme:'sand',read:'Checklist · 5 mục',lead:'Không cần nhớ mọi thứ trong đầu. Một danh sách ngắn sẽ giúp bạn chủ động hơn khi trao đổi với chủ nhà.',body:'<h3>Danh sách cho buổi hẹn của bạn</h3><ul><li>Xem lại địa chỉ và giờ hẹn đã được chủ nhà xác nhận.</li><li>Ghi sẵn các câu hỏi về không gian và sinh hoạt trong phòng.</li><li>Đánh dấu những tiện ích bạn muốn xem trực tiếp.</li><li>Ghi lại các thông tin chủ nhà trao đổi để đối chiếu sau buổi xem.</li><li>Để lại thời gian cân nhắc và chọn phương án phù hợp với mình.</li></ul>'},
        {id:2,category:'living',label:'Góc sống tiện nghi',title:'Một góc nhỏ, thêm chút dễ chịu.',excerpt:'Vài ý tưởng cho bàn học, góc đọc sách và những món đồ bạn yêu thích.',theme:'sage',read:'Góc cảm hứng',lead:'Một chiếc đèn, vài cuốn sách hay một chậu cây nhỏ có thể trở thành điểm nhấn cho góc riêng của bạn.',body:'<h3>Bắt đầu từ góc bạn dùng nhiều nhất</h3><p>Hãy nghĩ đến cách bạn muốn sử dụng không gian: học tập, đọc sách hay nghỉ ngơi. Giữ những món đồ thường dùng ở gần và dành một khoảng trống cho sinh hoạt hằng ngày.</p><h3>Chậm một chút cũng được</h3><p>Bạn có thể sắp xếp từng góc theo thời gian, thử nhiều cách bố trí và giữ lại điều khiến mình cảm thấy thoải mái.</p>'},
        {id:3,category:'visit',label:'Trong hành trình tìm phòng',title:'Lưu lại để dễ xem và so sánh.',excerpt:'Giữ những lựa chọn bạn quan tâm trong cùng một nơi.',theme:'blue',read:'Mẹo sử dụng EZ Rent',lead:'Khi thấy một căn phòng phù hợp, bạn có thể lưu lại để trở về xem sau mà không cần tìm kiếm từ đầu.',body:'<h3>Những lựa chọn của riêng bạn</h3><p>Nút trái tim giúp đưa phòng vào danh sách Đã lưu. Bạn có thể xem lại thông tin, mở chi tiết từng phòng và bỏ lưu khi đã thay đổi ý định.</p><h3>Khi đã muốn xem trực tiếp</h3><p>Vào chi tiết phòng để chọn một giờ đến do chủ nhà mở. Gửi yêu cầu vẫn cần chủ nhà xác nhận trước khi bạn đến xem.</p>'}
    ];
    function notices() {
        return state.records.filter(a=>['Pending','Confirmed'].includes(a.status)&&a.day>=0)
            .sort((a,b)=>a.status===b.status?b.id-a.id:a.status==='Confirmed'?-1:1)
            .map(a=>({id:'appointment-'+a.id,appointmentId:a.id,icon:'calendar',title:a.status==='Confirmed'?'Chủ nhà đã xác nhận lịch xem phòng':'Yêu cầu của bạn đang chờ phản hồi',text:a.status==='Confirmed'?`${time(a.start)} ngày ${date(a.day)}. Bạn có thể xem lại chi tiết lịch hẹn.`:`Giờ đến ${time(a.start)} ngày ${date(a.day)}. Giữ khung đến ${a.deadline}.`,time:'Hôm nay · 07:00'}))
            .concat({id:'welcome',icon:'home',title:'Chào mừng bạn đến với EZ Rent',text:'Tìm phòng, lưu lựa chọn và theo dõi lịch hẹn ngay tại đây.',time:'Bắt đầu hành trình của bạn'});
    }
    const unreadCount=()=>notices().filter(n=>!seen.has(n.id)).length;
    function noticeList(all=false) {
        return `<div class="home-notices">${(all?notices():notices().slice(0,2)).map(n=>`<button class="home-notice ${seen.has(n.id)?'':'unread'}" data-notice="${n.id}"><span class="notice-state">${icon(n.icon)}</span><span class="home-notice-copy"><strong>${esc(n.title)}</strong><span>${esc(n.text)}</span><time>${n.time}</time></span>${seen.has(n.id)?'':'<span class="notice-unread" aria-label="Chưa đọc"></span>'}</button>`).join('')}</div>`;
    }
    function checklist(a) {
        const items=[...a.body.matchAll(/<li>(.*?)<\/li>/g)].map(m=>m[1]);
        return `<div class="home-checklist"><div class="checklist-heading"><h4>Danh sách cho buổi hẹn của bạn</h4><output id="checklist-count" aria-live="polite">${checked.size}/${items.length} mục</output></div>${items.map((item,i)=>`<label class="checklist-item ${checked.has(i)?'done':''}"><input type="checkbox" data-checklist="${i}" ${checked.has(i)?'checked':''}><span>${item}</span></label>`).join('')}<p class="checklist-note">Đánh dấu để tự theo dõi trong lần xem thử này.</p></div>`;
    }
    function story(a) {
        const open=expandedArticle===a.id;
        return `<article class="home-story ${a.id===1?'featured':''}" id="article-${a.id}"><div class="home-story-copy"><h3>${a.title}</h3><p class="story-meta">${a.label} · ${a.read}</p><p class="story-excerpt">${a.excerpt}</p><button class="text-button article-toggle" data-article="${a.id}" aria-expanded="${open}" aria-controls="article-body-${a.id}">${open?'Thu gọn':a.id===1?'Mở checklist':'Đọc bài viết'}${icon('chevron-down')}</button></div><div class="home-article-body" id="article-body-${a.id}" ${open?'':'hidden'}><p class="home-article-lead">${a.lead}</p>${a.id===1?checklist(a):a.body.replaceAll('<h3>','<h4>').replaceAll('</h3>','</h4>')}</div></article>`;
    }
    function upcoming(next) {
        const r=next&&rooms.find(r=>r.id===next.roomId);
        return `<section class="home-upcoming" aria-labelledby="home-next-title"><div class="home-section-title"><h2 id="home-next-title">Lịch hẹn tiếp theo</h2>${next?`<span class="home-confirmed">${icon('check')}Đã xác nhận</span>`:''}</div>${r?`<div class="upcoming-detail"><div class="home-date-box"><span>Tháng 10</span><strong>${15+next.day}</strong><span>${['Thứ Năm','Thứ Sáu','Thứ Bảy','Chủ Nhật','Thứ Hai'][next.day]||date(next.day)}</span></div><div class="upcoming-info"><p class="home-upcoming-time">${time(next.start)}<span> · Giờ đến xem</span></p><h3>${esc(r.title)}</h3><p class="upcoming-address">${icon('pin')}${esc(r.address)}, ${esc(r.city)}</p></div><button class="button" data-home-appointment="${next.id}">Xem lịch hẹn${icon('arrow')}</button></div>`:`<div class="home-empty"><p>Bạn chưa có lịch được chủ nhà xác nhận.</p><button class="text-button" data-go="appointments">Theo dõi yêu cầu của bạn${icon('arrow')}</button></div>`}</section>`;
    }
    function home() {
        const pending=state.records.filter(a=>a.status==='Pending').length;
        const next=state.records.filter(a=>a.status==='Confirmed'&&a.day>=0).sort((a,b)=>a.day-b.day||a.start-b.start)[0];
        const chosen=articles.filter(a=>category==='all'||a.category===category);
        const saved=rooms.filter(r=>state.saved.has(r.id)).slice(0,2);
        const markup=`<div class="home-heading"><div><h1>Chào Minh Anh!</h1><p>Thông tin mới cho hành trình tìm nhà của bạn.</p></div><div class="home-heading-actions"><time datetime="2026-10-15">Thứ Năm, 15 tháng 10</time><button class="button primary" data-go="search">${icon('search')}Tìm phòng</button></div></div>
        <div class="home-layout"><div class="home-content">${upcoming(next)}
        <section class="home-notification-section" aria-labelledby="home-notifications-title"><div class="home-section-title"><h2 id="home-notifications-title">Thông báo gần đây${unreadCount()?`<span class="home-section-count" aria-label="${unreadCount()} thông báo chưa đọc">${unreadCount()}</span>`:''}</h2><button class="text-button" data-action="home-notifications">Xem tất cả${icon('arrow')}</button></div>${noticeList()}</section>
        <section class="home-news" aria-labelledby="home-news-title"><div class="home-section-title"><div><h2 id="home-news-title">Góc người thuê</h2><p>Đọc một chút, chủ động hơn khi tìm và ở.</p></div></div><nav class="home-categories" aria-label="Lọc bài viết">${[['all','Tất cả'],['visit','Đi xem phòng'],['living','Sống tiện nghi']].map(([key,label])=>`<button data-home-category="${key}" aria-pressed="${category===key}">${label}</button>`).join('')}</nav><div class="home-stories ${category==='all'?'':'filtered'}">${category==='all'?story(chosen[0])+`<div class="home-story-stack">${chosen.slice(1).map(story).join('')}</div>`:chosen.map(story).join('')}</div></section>
        </div><aside class="home-aside" aria-label="Tiện ích cho người thuê"><nav class="home-shortcuts" aria-label="Lối tắt Trang chủ"><h2>Của bạn</h2>
        <button class="home-shortcut" data-go="appointments">${icon('calendar')}<span><strong>Lịch hẹn</strong><small>${pending?pending+' yêu cầu đang chờ':'Theo dõi lịch của bạn'}</small></span>${icon('arrow')}</button>
        <button class="home-shortcut" data-action="saved">${icon('heart')}<span><strong>Phòng đã lưu</strong><small>${state.saved.size} phòng quan tâm</small></span>${icon('arrow')}</button>
        <button class="home-shortcut" data-action="home-messages">${icon('chat')}<span><strong>Tin nhắn</strong><small>Trao đổi với chủ nhà</small></span>${icon('arrow')}</button>
        <button class="home-shortcut" data-action="home-support">${icon('help')}<span><strong>Hỗ trợ</strong><small>Hỏi đáp và hướng dẫn</small></span>${icon('arrow')}</button></nav>
        <section class="home-saved" aria-labelledby="home-saved-title"><div class="home-section-title"><h2 id="home-saved-title">Đã lưu để xem lại</h2></div><div class="home-saved-list">${saved.map(r=>`<a href="?view=detail" class="home-saved-room" data-room="${r.id}"><span class="saved-room-symbol">${icon('room')}</span><span class="home-saved-copy"><strong>${esc(r.title)}</strong><span>${esc(r.district)} · ${r.area} m²</span><span class="saved-price">${money(r.price)} <small>/ tháng</small></span></span>${icon('arrow')}</a>`).join('')||'<p class="home-empty">Bạn chưa lưu phòng nào. Khi tìm thấy lựa chọn phù hợp, bấm trái tim để xem lại tại đây.</p>'}</div>${saved.length?`<button class="text-button" data-action="saved">Xem danh sách đã lưu${icon('arrow')}</button>`:''}</section>
        <section class="home-help"><h2>Cần trao đổi thêm?</h2><p>Xem hướng dẫn tìm phòng, theo dõi lịch hẹn và cách sử dụng EZ Rent.</p><button class="text-button" data-action="home-support">Mở phần hỗ trợ${icon('arrow')}</button></section></aside></div>
        <footer class="home-footnote"><span>EZ Rent · Không gian của bạn</span><span>Bản xem trước · Nội dung minh họa</span></footer>`;
        if(!mobileLayout.matches)return markup;
        const template=document.createElement('template'); template.innerHTML=markup;
        const layout=template.content.querySelector('.home-layout');
        const blocks=['.home-upcoming','.home-notification-section','.home-shortcuts','.home-news','.home-saved','.home-help'].map(selector=>layout.querySelector(selector));
        layout.replaceChildren(...blocks); return template.innerHTML;
    }
    document.addEventListener('change',event=>{
        const input=event.target.closest('[data-checklist]'); if(!input)return;
        const i=Number(input.dataset.checklist); input.checked?checked.add(i):checked.delete(i);
        input.closest('label').classList.toggle('done',input.checked);
        $('#checklist-count').textContent=`${checked.size}/5 mục`;
    });
    function toggleArticle(id) {
        if(!articles.some(a=>a.id===id))return;
        if(state.view!=='home'||!document.getElementById('article-'+id)){
            category='all'; state.view='home'; expandedArticle=id; render(); return;
        }
        expandedArticle=expandedArticle===id?null:id;
        document.querySelectorAll('.home-story').forEach(story=>{
            const toggle=story.querySelector('[data-article]'); const open=Number(toggle.dataset.article)===expandedArticle;
            toggle.setAttribute('aria-expanded',String(open));
            toggle.innerHTML=(open?'Thu gọn':Number(toggle.dataset.article)===1?'Mở checklist':'Đọc bài viết')+icon('chevron-down');
            story.querySelector('.home-article-body').hidden=!open;
        });
    }
    function showDialog(title,body) {
        const dialog=$('#home-dialog');
        $('#home-dialog-title').textContent=title;
        $('#home-dialog-content').innerHTML=body;
        if(!dialog.open){dialogOpener=document.activeElement;dialog.showModal();}
    }
    function showNotices() {showDialog('Thông báo của bạn',noticeList(true)+`<div class="home-notices-footer"><button class="button" data-action="home-read-all" ${unreadCount()?'':'disabled'}>Đánh dấu tất cả đã đọc</button></div>`)}
    function openAppointment(id) {
        if(!state.records.some(a=>a.id===id))return;
        $('#home-dialog').close();state.selected=id;state.filter='all';state.focused=true;state.view='appointments';render();scrollTo(0,0);
    }
    function click(button,event) {
        if(button.dataset.article){
            event.preventDefault(); const fromDialog=$('#home-dialog').open;
            if(fromDialog){dialogOpener=null;$('#home-dialog').close();expandedArticle=null;}
            toggleArticle(Number(button.dataset.article));
            if(fromDialog){const target=document.querySelector(`[data-article="${button.dataset.article}"]`);target?.focus();target?.scrollIntoView({block:'center'});}
            return true;
        }
        if(button.dataset.homeCategory){category=button.dataset.homeCategory;const y=scrollY;render();document.querySelector(`[data-home-category="${category}"]`)?.focus({preventScroll:true});scrollTo(0,y);return true;}
        if(button.dataset.homeAppointment){openAppointment(Number(button.dataset.homeAppointment));return true;}
        if(button.dataset.notice){const n=notices().find(x=>x.id===button.dataset.notice);if(!n)return true;seen.add(n.id);if(n.appointmentId)openAppointment(n.appointmentId);else{render();showDialog(n.title,`<p class="home-dialog-empty">${n.text}</p><button class="button primary" data-action="home-find">Tìm phòng${icon('arrow')}</button>`);}return true;}
        switch(button.dataset.action){
            case 'home-notifications':showNotices();return true;
            case 'home-read-all':notices().forEach(n=>seen.add(n.id));render();showNotices();$('#home-dialog .close').focus();return true;
            case 'home-find':$('#home-dialog').close();state.savedOnly=false;go('search');return true;
            case 'home-messages':showDialog('Tin nhắn',`<p class="home-dialog-empty">Bạn chưa có cuộc trò chuyện nào. Bạn có thể trao đổi với chủ nhà trong quá trình tìm phòng hoặc liên hệ EZ Rent khi cần hỗ trợ.</p><div class="home-help-links"><button class="button" data-action="home-find">Tìm phòng để liên hệ${icon('arrow')}</button><button class="button" data-action="home-support">Hỗ trợ từ EZ Rent${icon('arrow')}</button></div>`);return true;
            case 'home-support':showDialog('EZ Rent có thể giúp gì cho bạn?',`<p class="home-dialog-empty">Chọn nội dung bạn muốn tìm hiểu.</p><div class="home-help-links"><button class="button" data-article="1">Chuẩn bị cho buổi xem phòng${icon('arrow')}</button><button class="button" data-article="3">Lưu và theo dõi phòng quan tâm${icon('arrow')}</button><button class="button" data-go="appointments" data-close-home>Lịch hẹn và phản hồi chủ nhà${icon('arrow')}</button></div>`);return true;
        }
        return false;
    }
    return {home,click,unreadCount};
};
