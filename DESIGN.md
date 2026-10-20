---
name: EZ Rent Tenant Home
description: Visual rules for the approved Tenant Home v3 and its MVC implementation.
colors:
  primary: "#356797"
  primary-hover: "#2a537b"
  background: "#f6f7f9"
  surface: "#ffffff"
  text: "#1f2f40"
  muted: "#586779"
  divider: "#dce2e9"
  appointment: "#eaf0f6"
typography:
  headline:
    fontFamily: "Segoe UI, Arial, sans-serif"
    fontSize: "32px"
    fontWeight: 700
    lineHeight: 1.25
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Segoe UI, Arial, sans-serif"
    fontSize: "20px"
    fontWeight: 650
    lineHeight: 1.4
  body:
    fontFamily: "Segoe UI, Arial, sans-serif"
    fontSize: "15px"
    lineHeight: 1.65
  label:
    fontFamily: "Segoe UI, Arial, sans-serif"
    fontSize: "13px"
    fontWeight: 600
rounded:
  control: "8px"
  panel: "14px"
spacing:
  compact: "8px"
  normal: "16px"
  section: "36px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.control}"
    padding: "11px 16px"
    height: "46px"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
  appointment-panel:
    backgroundColor: "{colors.appointment}"
    rounded: "{rounded.panel}"
    padding: "23px 24px"
---

# Design System: EZ Rent Tenant Home

## Overview

This records Home v3 and the MVC implementation approved by Dương. It does not replace the design of every EZ Rent screen. Production sources: wwwroot/css/tenant-home.css, wwwroot/js/tenant-home.js, Areas/Tenant/Views/Dashboard/Index.cshtml and its shared layout/partials. Original reference: docs/tenant-home-outline.css and docs/tenant-home-outline.js. P3 blue and the established sans remain. Information, state and actions carry the page; the new design removes the decorative banner and scene illustrations from Home.

This is an authenticated app surface. It prioritizes confirmed appointments and notices, then useful reading. The user requested a separate Home with only a small room section. No metaphor or new brand direction has been claimed as user approved.

## Colors

Primary blue marks actions and current choices. Neutral text and dividers structure the remaining content. The appointment panel uses a pale blue background; confirmed status also uses a semantic green text treatment. Actual CSS is authoritative.

## Typography

One family serves headings, labels and prose. Desktop greeting is 32px; mobile is 28px. Section titles are 20px, mobile 19px. Featured article title is 22px. Main copy is 14–15px, notice detail is 13px, metadata is 12px. The appointment time is 27px with tabular numerals. No display font is introduced into app controls.

## Layout

Home container is at most 1280px. At 1200px and above it has content and a 280px utility rail separated by 48px. At 1000–1199px the rail is 245px and the gap is 28px. Below 1000px it becomes one reading order: next confirmed visit, notices, shortcuts, articles, saved items and help. Below 768px shortcuts use two columns and article sections stack. Mobile padding is 20px, reduced to 16px below 360px. Main sections are spaced 36px on desktop, 28px on mobile.

Home uses a sticky top header at top:0, z-index:20, with an opaque white background. Header height is 85px including its rule, 73px below 768px. Scroll padding at the top is 104px desktop/tablet and 88px mobile, preserving access to anchor targets beneath it.

## Elevation & Depth

Flat surfaces and dividers. No Home content shadows. The appointment panel uses tonal separation, the checklist article a single thin border. Dialog backdrop is a simple translucent overlay without decorative blur.

## Shapes

Content panels use 14px corners; primary controls 8px. The calendar date tile uses 10px corners. Do not infer a prohibition on existing rounded controls in other screens from this scoped redesign.

## Components

- Primary action: Find room, visible beside the greeting on desktop and below it on mobile.
- Confirmed visit: date/time, room/address and a direct detail action. Pending requests do not populate this panel.
- Notices: two recent rows, unread dots and a complete list through the bell. In MVC, read state is stored in owner-scoped Notification records via CSRF-protected POST; the bell opens a separate paginated page. The standalone outline retains its local demo state.
- Reading: underlined category selection and inline expand/collapse. Articles start collapsed. The first article contains five native checkbox labels; MVC uses native details/summary and preserves state while resizing by moving existing nodes. Checkboxes are a preparation aid, not server records.
- Utility rail: plain rows for appointments, saved rooms, messages and help. At most two saved rooms.
- Focus: 3px primary-blue outline with 4px offset. The article chevron changes over 180ms; reduced-motion removes that transition.

## Do's and Don'ts

- Do retain clear status wording and readable action labels.
- Do maintain DOM reading order when the layout changes.
- Do distinguish preview content from real tenant data.
- Don't reintroduce the decorative hero, repeated pastel shortcut tiles or scene SVGs into this Home proposal.
- Don't expand this file into a global redesign requirement without agreement. Other preview screens and MVC modules retain their existing designs. The MVC Home also handles long unbroken titles and full-width panels under 1000px.


## Tin nhắn MVC — phạm vi riêng đã duyệt, 16/10/2026

Thiết kế Tin nhắn được Dương thích và duyệt ghép MVC cho cả ba kích thước. Phạm vi này dùng `tenant-messages.css`, `tenant-messages.js`, `_TenantMessagesLayout` và các view Dashboard/Messages. Các token Home ở trên giữ nguyên cho Home; không áp font/phong cách Tin nhắn sang toàn ứng dụng.

Tin nhắn: xanh #356797, chữ #203246, phụ #607085, nền #f4f6f9, trắng #ffffff, đường chia #e0e6ed. Be Vietnam Pro local 400/600. Khung tối đa 1440px; desktop trên 1240px ba cột 292px / co giãn / 268px, tablet hai cột, mobile một màn hộp thư hoặc chat. Header desktop tái sử dụng component chung; mobile có nav dưới chỉ ở hộp thư. Giữ bản nháp, phòng liên quan và nút gửi chưa khả dụng đúng sự thật. Thông tin phòng dùng ảnh của tin; không thay bằng stock production. Chi tiết ranh giới và QA: docs/tenant-messages-mvc.md.


## Tài khoản Tenant — 17/10/2026

Trang Cá nhân/Bảo mật kế thừa màu, font và shared header của Home, không tạo shell mới. Nội dung tối đa 1080px; form chính và rail ảnh/bảo mật 320px, giảm 280px dưới 1000px; dưới 768px xếp một cột. Nhãn và lỗi nằm cạnh trường, input 16px, nút tối thiểu 44px; preview ảnh ghi rõ chưa lưu. File riêng tenant-account.css/js, chỉ tải ở trang tài khoản.


## Phòng đang ở — 17/10/2026

Gói kế thừa thế giới thị giác Home/tài khoản: Segoe UI, xanh #356797, nền #f6f7f9, trắng và đường chia #dce2e9. Phần mới dùng tenant-rental.css, không thay font hay hình học header chung. Tổng quan một phòng: ảnh thật của phòng cạnh thông tin hợp đồng; tổng cần theo dõi và hai danh sách ngắn nằm dưới. Danh sách dùng dòng có đường chia, tránh lặp thẻ trang trí. Trang chi tiết/form có nội dung chính và rail 320px (260px dưới 1000px), xếp một cột dưới 768px. Khung ảnh không quyết định chiều cao toàn trang.

Mở lối Phòng đang ở trên header chung; mobile dùng partial đích đến chung cho Home, tìm phòng và tin nhắn, giữ ẩn menu trong màn chat. Màu trạng thái đi cùng nhãn, nút tối thiểu 44px, input 16px, lỗi cạnh trường, hủy có trang xác nhận. Phạm vi và kết quả tại docs/tenant-my-room-mvc.md.

## Bản thử tài liệu — phạm vi riêng, 17/10/2026

docs/tenant-documents-preview.html/css/js kế thừa palette và CSS header, chưa thay giao diện MVC hiện có. Desktop đặt form cạnh mẫu giấy; tablet chia nhóm thông tin thành hai cột rồi tới mẫu; mobile xếp theo thứ tự điền → xem mẫu → tài liệu. Mẫu giấy dùng Times New Roman để giữ dấu tiếng Việt khi in, giao diện giữ Segoe UI. Hóa đơn có hai mục điện/nước riêng, ảnh trống có lời giải thích, ảnh được chọn mở bằng dialog native có Escape và trả focus. Khi ghép MVC phải tái sử dụng component header chung.

## Tài liệu MVC và BM03 — 17/10/2026

Đã ghép thành phần vào layout Tenant chung, không sao lại header. Khu tài liệu nằm dưới thông tin hợp đồng, lịch sử phân trang số, xác nhận gắn với phiên bản. Hóa đơn có hai ảnh độc lập và dialog ảnh dùng bàn phím được. BM03 nằm ở trang nháp riêng, giấy Times New Roman, giữ ba phần/trang theo nguồn; in chỉ có giấy, menu và nút ẩn. Mẫu gốc được giữ nguyên; bản nháp đổi nước sang m³ theo quyết định của Dương. Dữ liệu thiếu hiển thị trống và lời giải thích thay vì điền giả. Chưa thiết kế form Tenant thay thông tin bên Landlord.


## Form thông tin BM03 — 17/10/2026

Form Tenant nằm trước giấy, nhóm cá nhân/giấy tờ, hai cột từ 640 px và một cột ở mobile. Giữ font/palette/component chung, input 16 px và nút 44 px; lỗi cạnh trường và giữ dữ liệu khi lỗi. Có liên kết đến giấy, nhắc chưa lưu, nút in chỉ dùng bản đã lưu. Lịch sử dùng danh sách và số trang; bản cũ chỉ đọc. Điều khoản nguồn ở phần đối chiếu và phụ lục in riêng. Mục thiếu phía Landlord được ghi rõ, không cho Tenant sửa thông tin của chủ nhà.
