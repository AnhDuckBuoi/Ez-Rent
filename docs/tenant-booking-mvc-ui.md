# Ghép giao diện đặt lịch vào MVC — 16/10/2026

## Phạm vi Dương đã chọn

Sau review bản thử desktop, Dương yêu cầu bước tiếp theo và chọn rõ: **“Chỉ ghép giao diện Tenant; tôi tự phối hợp dữ liệu và Landlord.”** Vì vậy gói này đưa màn chọn lịch vào ứng dụng theo đúng phòng đang xem, cung cấp điểm nối đọc lịch và xem trước yêu cầu, không triển khai gửi/giữ lịch thật.

Không đổi model dùng chung, enum, DbContext, migration, Program.cs hoặc phần Landlord. Không áp dụng migration/database nhóm. Nhánh `feature/tenant-p3`, chưa commit/push.

## Luồng hiện có

Tìm phòng → Chi tiết phòng bất kỳ còn công khai/trống → **Đặt lịch xem phòng** → `/Tenant/ViewingBooking/Index/{id}` → chọn ngày/giờ → **Xem trước yêu cầu**. Phòng, giá, địa chỉ, ảnh và người đăng lấy đúng từ dữ liệu phòng, không cố định phòng minh họa số 3. Nút đặt lịch là hành động chính, Lưu phòng là phụ. Menu Lịch hẹn vẫn dẫn đến danh sách lịch hiện có; ở màn chọn giờ, menu Tìm phòng được đánh dấu.

Hộp xem trước hiển thị phòng, giờ đến, ngày Việt Nam, địa chỉ và ghi chú. Nó ghi rõ **yêu cầu chưa gửi, không tạo lịch hẹn**; không giả làm Pending và không thêm mục vào Lịch hẹn. Giới hạn 2 giờ là nội dung hướng dẫn nghiệp vụ, chưa phải hạn giữ backend.

URL quay về luôn là Chi tiết cùng phòng, bên trong giữ đường dẫn danh sách/bộ lọc/phân trang ban đầu. `returnUrl` ngoài site bị bỏ qua. Chọn ngày là GET có liên kết thật; JS nâng cấp sang tải nội dung tại chỗ, giữ ghi chú trong bộ nhớ trang và hỗ trợ Back/Forward. Không lưu ghi chú vào URL, sessionStorage hoặc database. Tải lại/rời trang có thể mất bản nháp.

## Điểm nối cho Dương

`ITenantViewingScheduleReader.ReadAsync(postId, tenantId, vietnamDate, cancellationToken)` chỉ đọc nguồn khung giờ. ID Tenant do controller lấy từ đăng nhập. Khi nguồn thật sẵn sàng, đăng ký triển khai bằng DI; constructor controller cho phép thiếu reader để ứng dụng hiện tại vẫn chạy an toàn mà không cần chỉnh Program.cs ở gói này.

Kết quả:

- `TenantScheduleState`: NotConnected, Open, Unopened, Full, Error (trạng thái trình bày, không phải enum lịch hẹn dùng chung).
- `TenantViewingSlot`: ID nguồn, `ArrivalAt` có offset, trạng thái Available/Booked/Held/HeldByMe. Không đưa danh tính người giữ khác ra trang.
- `IsDemo`: chỉ để gắn nhãn nguồn minh họa. Nguồn thật phải trả false.

Controller đọc tin Approved + Available trước khi gọi reader. Sau đó lọc slot đúng ngày Việt Nam, thời điểm ≥ thời gian server + 6 giờ, loại ID trống/trùng, sắp xếp giờ đến. Lọc này phục vụ hiển thị, **không thay thế kiểm tra khi gửi thật**. Không có nguồn thì không tự sinh giờ; hiện “Lịch xem phòng đang được cập nhật”. Có trạng thái kín/chưa mở/lỗi tải và liên kết thử lại/chọn ngày khác. Ngày sai định dạng hoặc quá khứ trả 400 có đường phục hồi, phòng không khả dụng trả 404.

Khi Dương ghép phần gửi: bổ sung POST có CSRF, nguồn slot có thẩm quyền, kiểm tra phòng và 6 giờ tại lúc gửi, idempotency/cạnh tranh theo người dẫn + giờ đến, giữ 2 giờ bằng dữ liệu server, xác nhận/hết hạn/hủy nguyên tử, rồi redirect chi tiết yêu cầu và đưa vào danh sách. Cần đồng thời thay hoặc chặn Create/Edit cũ nhận giờ tự do. **Các action cũ đó vẫn tồn tại trong gói giao diện này**, chưa được coi là hoàn tất backend theo chính sách slot. Xem `tenant-appointment-data-contract.md`.

## Bản chạy thử tại 8770

Host kiểm thử độc lập đăng ký `PreviewViewingScheduleReader` trong dự án tests, chỉ khi chạy `--preview`. Không đăng ký reader này trong ứng dụng chính. Nguồn giờ minh họa theo ngày đang chọn và ID phòng; ngày +2 minh họa kín, +3 chưa mở, +4 lỗi tải (lặp theo tuần). Hôm nay có thể không còn giờ đủ 6 tiếng; chọn ngày mai để thử chọn giờ. Ngày/giờ hiện tại là thời gian thật của host; không dùng đồng hồ 07:00 cố định của HTML prototype.

Cả banner host lẫn màn đặt lịch ghi rõ dữ liệu mẫu, xem trước không gửi thật. Màn ứng dụng không có nguồn sẽ hiển thị trạng thái đang cập nhật, không lấy lịch mẫu của host tests. Bản HTML 8766 giữ riêng làm tài liệu review trước đó.

## Xác minh

- **119/119 kiểm tra đạt** (`tenant-booking-ui.trx`), gồm 107 trước đó + 12 trường hợp về xác thực mới, phòng không khả dụng, đúng phòng/return URL/menu, không có nguồn không sinh giờ, POST vào màn GET bị 405, số lịch không đổi, 6 giờ chính xác/thiếu 1 giây/ngày Việt Nam/ID trùng và ngày sai/quá khứ.
- Build qua, 16 cảnh báo nullable cũ ngoài phần Tenant thay đổi, không có cảnh báo mới của màn này.
- Trình duyệt: luồng với phòng 3/10/17, hộp xem trước đúng thông tin, ghi chú chứa HTML hiển thị dạng văn bản, Esc khôi phục focus, trạng thái kín/chưa mở/lỗi, đổi ngày/Back/Forward và đường quay lại giữ bộ lọc. Kiểm tra kích thước 320/390/834/1024/1199/1440/1920 không tràn ngang hay ID trùng. Có fallback hẹp, chưa thiết kế lại tablet/mobile.
- Mất mạng khi đổi ngày giữ ghi chú, có thông báo lỗi và thử lại được. Tổng 14 nhóm kiểm tra trình duyệt đạt; không có lỗi JS hoặc request ghi. Số lịch hẹn trước/sau bằng nhau (5 bản ghi mẫu).
- Kiểm tra bổ sung nút chưa chọn giờ dùng nền xám đúng, header desktop sticky; đã xem ảnh desktop/hộp xem trước và chụp lại ảnh sạch sau kiểm tra.
- Ảnh và `checks.json` nằm trong artifact `tenant-booking-mvc` của chat. Hash Program.cs giữ nguyên `A7C65A326EE5C6703B7A21ADE59B4009AAB6D98BD9440357A48C105465D1E64E`.

## Cách review ngay trong ứng dụng

Mở `http://127.0.0.1:8770/Tenant/RentalPost/Details/10`, bấm Đặt lịch xem phòng, chọn ngày mai, chọn giờ có thể chọn, thêm ghi chú, bấm Xem trước yêu cầu. Đóng hộp, đổi ngày rồi quay về Chi tiết phòng để kiểm tra thông tin vẫn đúng. Lịch hẹn trên menu tiếp tục hiển thị các lịch có sẵn, không thêm bản xem trước.
