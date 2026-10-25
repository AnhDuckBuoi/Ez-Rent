# Tenant v4 — thử hướng sáng, hiện đại, ảnh chụp

16/10/2026. Dương chưa ưng v3, yêu cầu tìm skill khác và thử lại. Đã chọn “Sáng, hiện đại, có ảnh thực tế” qua câu hỏi trong phiên. Skill dùng: Frontend Design gốc của Anthropic, đã cài bằng skill-installer và đọc toàn bộ SKILL.md. Không áp hướng này vào MVC hoặc DESIGN.md chung trước khi review bản thử.

## Kế hoạch thiết kế theo skill

Mục đích: Trang chủ người thuê để theo dõi lịch, thông báo và đọc nội dung hữu ích. Phòng chỉ là phần nhỏ, Tìm phòng là trang riêng. Giữ EZ Rent, màu xanh và header ghim. Phạm vi bản thử: Trang chủ, thông báo, đọc nội dung thông báo; liên kết khám phá/lịch/phòng dẫn tới MVC chạy thử sẵn có, thông báo tương tác nội bộ bản thử.

Palette: xanh EZ Rent #356797, xanh đậm chữ #1d3041, trắng bề mặt #ffffff, nền sáng #f5f7fa, chữ phụ #526779, đường biên #e1e7ee. Xanh lá chỉ dùng trạng thái đã xác nhận. Màu ấm xuất phát từ ảnh chụp, không phủ màu kem/cam lên toàn ứng dụng.

Type: Be Vietnam Pro tự host, Regular 400 và SemiBold 600. Heading 30px/24px, body 14–15px, metadata 12px. Không dùng serif/monospace để tạo cảm giác thương hiệu khác P3.

Layout, căn trái:

```text
Header ghim: EZ Rent | Trang chủ, Tìm phòng, Lịch hẹn, Đã lưu | Chuông
Lời chào                                                Tìm phòng
Nội dung chính                                Cột phụ
  Lịch đã xác nhận gọn                          Thông báo mới
  Góc người thuê                                Lối tắt
  Ảnh đời sống + bài nổi bật                     Hai phòng đã lưu nhỏ
  Hai hướng dẫn nhỏ                             Hỗ trợ
```

Mobile: lời chào → lịch → bài có ảnh → thông báo → hướng dẫn thêm → tiện ích/phòng đã lưu. Chuông luôn hiện để mở thông báo ngay. Dời node thay vì render lại khi đổi breakpoint để giữ trạng thái đọc/checklist. Thông báo có lọc tất cả/chưa đọc, đánh dấu đã đọc, trạng thái rỗng và mở đúng nội dung minh họa.

## Tự phản biện trước khi dựng

Chỉ thêm ảnh vào v3 vẫn giữ cấu trúc danh sách khô và chữ nhỏ. Vì vậy thiết kế lại cả tỷ lệ cột, chữ, chiều cao lịch và cách đọc bài; không chỉ thay CSS màu. Cũng không quay lại banner quảng cáo lớn hoặc lưới mọi phòng. Điểm nhấn là một ảnh biên tập có nội dung liên quan; các khu tác vụ giữ yên và quen thuộc. Không dùng ảnh stock làm bằng chứng phòng cho thuê thật.

## Nguồn tài nguyên

- living.jpg: Caleb Oquendo / Pexels, https://www.pexels.com/photo/living-room-interior-with-sofa-against-window-at-home-7513991/
- window.jpg: cottonbro studio / Pexels, https://www.pexels.com/photo/a-plant-placed-on-the-window-10254323/
- preparation.jpg: kaboompics / Pexels, https://www.pexels.com/photo/wooden-table-with-coffee-and-notebook-with-pen-4195334/
- Ảnh thuộc các trang “Free to use”; tải bản kích thước web từ images.pexels.com. Không phải ảnh phòng từ database nhóm. Nguồn ảnh ghi ở chân bản thử.
- Be Vietnam Pro: https://github.com/google/fonts/tree/main/ofl/bevietnampro — giữ OFL.txt cùng font. Tất cả tài nguyên phục vụ từ docs/tenant-v4-assets; khi xem không gọi mạng bên ngoài.

## Phạm vi bảo toàn

Không thay controller, Razor, CSS MVC, dữ liệu, schema hoặc Figma. Không commit/push. v3/MVC và các preview trước vẫn tồn tại để so sánh. Không thay luật đặt lịch hoặc coi UC09/UC10 đã hoàn tất từ một nút trên bản thử.


## Bản để review

- Trang chủ: http://127.0.0.1:8766/tenant-v4-preview.html
- Thông báo: http://127.0.0.1:8766/tenant-v4-preview.html?page=notifications
- Bấm “So với bản hiện tại” để mở đúng Home/Notification MVC ở 8770 trong tab khác.
- Chọn “Tài khoản mới” ở thanh bản thử để xem trạng thái rỗng. Thông báo, bộ lọc, checklist và lịch sử quay lại trong bản thử chỉ là trạng thái bộ nhớ; tải lại khôi phục ví dụ. Ngày/giờ ví dụ của bản thử cố định để review, có thể khác dữ liệu mẫu MVC khởi tạo theo ngày chạy.
- Các liên kết tìm phòng/lịch/phòng đã lưu/hỗ trợ mở MVC có sẵn bằng GET. UC09/UC10 và các mục chưa hoàn thiện vẫn theo phạm vi của MVC, không được xem là đã triển khai từ bản thử này.

## Kiểm tra hoàn tất

Đã đọc và áp dụng Frontend Design; không dùng pipeline Impeccable cho lần thử này. Bản đầu chỉ có thay hình vẫn giữ khoảng cách lớn dưới chủ đề, nên gom tiêu đề/chủ đề và bài nổi bật thành cùng một nhóm để sửa nhịp đọc. Giữ phần tác vụ gọn, không thêm carousel, số liệu thương mại hay banner bán hàng.

Chromium: ba màn Home/Notification/Detail tại 320/390/768/834/938/1024/1440px (21 tổ hợp), không tràn ngang, ID trùng hoặc lỗi JavaScript; ảnh tải được. Đã xem ảnh desktop, kích thước 938px và mobile. Font 400/600 đều được tải thực tế. Có kiểm tra chủ đề bài viết, details/checklist, đổi breakpoint giữ trạng thái/đúng thứ tự đọc, lọc chưa đọc, mở và đánh dấu thông báo, đọc tất cả, trạng thái rỗng, link checklist từ inbox, header sticky và browser Back/Forward. Tải trực tiếp URL chi tiết cũng đồng bộ trạng thái đã đọc trong bản mẫu.

Không có request ghi hoặc request tài nguyên ngoài localhost trong các luồng bản thử. MVC cũ đã được khởi động lại bằng test executable/SQLite riêng (không chạy Program.cs chính, không dùng database nhóm) để các link so sánh mở được. Không thay CSS MVC, controller, Razor, model hoặc migration; không chạy lại test C# cho thay đổi tài liệu/HTML/CSS/JS tĩnh này. Chưa commit/push.
