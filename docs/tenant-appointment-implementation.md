# Gói 3B — Lịch hẹn của tôi

> Cập nhật 16/10/2026: nút Chi tiết phòng đã mở được trang đặt lịch MVC theo đúng phòng; gói này chỉ giao diện/xem trước, chưa gửi lịch. Xem [tenant-booking-mvc-ui.md](tenant-booking-mvc-ui.md). Các phần bên dưới là ghi nhận giai đoạn trước.


Dương yêu cầu nêu bước tiếp theo và tiếp tục thực hiện sau khi review gói 3A. Gói này dựng màn theo dõi lịch thật từ `ViewingAppointment` hiện có, cùng ngôn ngữ giao diện Tenant. Không thay model, enum, DbContext, migration hoặc database nhóm.

## Phạm vi

- Danh sách chỉ lịch của Tenant đang đăng nhập, đủ 5 bộ lọc trạng thái và số lượng của riêng mình. Phân trang 10 mục; ưu tiên lịch chờ/đã xác nhận theo giờ tăng dần, sau đó lịch sử theo giờ giảm dần, ID phân xử khi trùng giờ.
- Chi tiết hiển thị ngày/giờ, trạng thái, ghi chú của Tenant và phản hồi chủ nhà bằng văn bản được encode; không hiển thị ID tài khoản. `Pending` nhắc chờ xác nhận trước khi đến.
- Nếu tin phòng bị ẩn/nháp/đã thuê, giữ lịch và phản hồi nhưng che thông tin tin hiện tại, không có link chi tiết tin không công khai.
- Hủy có trang xác nhận và CSRF; chỉ `Pending`/`Confirmed` theo quy tắc trạng thái đã có. Giữ row và phản hồi, chuyển `Cancelled`, quay lại chi tiết. Kiểm tra trạng thái trong câu lệnh cập nhật; thông báo 409 có lối xem lại khi trạng thái vừa đổi. Không tự thêm cutoff theo giờ chưa được chốt.
- Danh sách rỗng toàn bộ khác với bộ lọc không có kết quả. Query sai trả 400 có đường quay lại; lịch không tồn tại/khác chủ đều trả 404 với cùng thông báo. Có lối tắt Lịch hẹn trên header tìm phòng/yêu thích; Dashboard đã có lối vào.

## Giới hạn cần biết

Luồng mới chưa có thao tác đặt/đổi giờ thật vì thiếu nguồn khung giờ Landlord. Nút đặt lịch ở chi tiết phòng vẫn chưa được bật. Các action Create/Edit cũ từ gói bảo vệ dữ liệu vẫn tồn tại, không được liên kết từ giao diện mới; chúng chưa áp dụng giới hạn 6 giờ hoặc cơ chế slot, nên chưa thể coi toàn bộ backend đặt lịch đã đáp ứng phương án mới. Khi triển khai slot phải thay thế/chặn đường ghi cũ để không bỏ qua quy tắc.

Ngày/giờ lịch cũ đang hiển thị theo giá trị đã lưu, chưa chuyển múi giờ vì source chưa có hợp đồng thống nhất. Xem [đề xuất dữ liệu/API](tenant-appointment-data-contract.md) để chốt cùng Đức. Ứng dụng chưa có lịch sử sự kiện nhiều bước; gói này chỉ giữ bản ghi và hai ghi chú có sẵn.

## Review trên bản chạy thử

Mở `http://127.0.0.1:8770/Tenant/ViewingAppointment`. Đây là MVC/controller/Razor thật chạy trong host kiểm thử riêng, database SQLite trong bộ nhớ, một Tenant mẫu, không gửi đến Landlord hoặc SQL Server nhóm. Ngày mẫu tính từ ngày khởi động host; không phải lịch chủ nhà thật.

1. Chuyển Tất cả/Chờ xác nhận/Đã xác nhận/Đã hoàn tất/Đã hủy/Bị từ chối; đối chiếu số lượng và nội dung.
2. Vào lịch chờ: thấy lời nhắc chờ; lịch xác nhận có phản hồi chủ nhà. Lịch hoàn tất/hủy/từ chối không có thao tác hủy.
3. Vào lịch chờ hoặc xác nhận → Hủy → Giữ lịch hẹn để quay lại. Sau đó thử Xác nhận hủy: trạng thái đổi, ghi chú vẫn còn và mục chuyển sang bộ lọc Đã hủy.
4. Thử `?Status=999`, `/Details/9999` và `/Details/2` (lịch Tenant khác) để xem thông báo lỗi. Tải lại vẫn giữ thay đổi trong phiên host; khởi động lại host khôi phục dữ liệu mẫu.
5. Thu cửa sổ xuống mobile, thao tác bộ lọc ngang và kiểm tra nội dung/điều khiển gần thanh dưới. Kiểm tra thích nghi màn hình rộng chưa thay thế việc review outline tablet/desktop riêng.

## Kiểm tra

84/84 kiểm thử tích hợp đạt: gồm 73 ca trước và 11 ca mới về bộ lọc/count/paging chỉ theo Tenant, query sai, danh sách rỗng, encode hai ghi chú, che tin không công khai, trạng thái đổi giữa mở form và gửi hủy, hủy lịch xác nhận giữ bản ghi và chặn hủy lại. Kiểm thử dùng SQLite riêng, không chạy database nhóm. Build thành công; 16 cảnh báo nullable có sẵn ở EmailSender và view ngoài Tenant còn nguyên.

Đã kiểm tra bằng Chromium luồng lọc → chi tiết → giữ lịch/hủy → trạng thái mới, phản hồi, lỗi 400/404/409, danh sách rỗng và lối vào từ tìm phòng. Bộ lọc và chi tiết vẫn hoạt động khi tắt JavaScript. Danh sách/chi tiết ở 320/390/768/1280 px không tràn ngang; không có lỗi JavaScript trong các luồng đã thử. Tab đang chọn tự cuộn vào vùng nhìn thấy trên mobile; thứ tự Tab của nút giữ/hủy khớp hiển thị; nút gửi khóa trong lúc xử lý và mở lại khi quay về trang qua `pageshow`. Đã xem ảnh danh sách, chi tiết và xác nhận hủy trên mobile. Bản chạy thử được khởi động lại sau kiểm thử để khôi phục mẫu.

Thay đổi vẫn ở nhánh `feature/tenant-p3`, chưa commit/push. `Program.cs` giữ nguyên SHA256 trước gói này; không sửa các controller Landlord.
