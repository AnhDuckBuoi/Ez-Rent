# Review luồng đặt lịch desktop — 16/10/2026

## Phạm vi đã duyệt

Dương đồng ý giữ **Lịch hẹn trên menu** để xem/quản lý lịch và đặt nút **Đặt lịch xem phòng trong Chi tiết phòng** để đi thẳng đến trang đặt lịch cho chính phòng đó. Bước hiện tại chỉ dựng bản thử desktop để review; chưa ghép nguồn lịch thật, chưa bật nút đặt lịch trong MVC.

File: `tenant-booking-desktop-preview.html`, CSS/JS cùng tên và `tenant-booking-room.svg` sao chép ảnh thay thế hiện có trong ứng dụng. Địa chỉ review: http://127.0.0.1:8766/tenant-booking-desktop-preview.html . Bắt đầu tại chi tiết phòng minh họa. Có thể mở trực tiếp màn chọn giờ bằng `?view=book`.

## Quyết định giao diện

- Giữ khung desktop đã thống nhất, header ghim 85px, nội dung tối đa 1280px. Không thêm nút đặt nhanh ở danh sách tìm phòng trong bản này.
- Dùng màu hiện có: nền #f6f7f9, bề mặt #ffffff, chữ #1f2f40, chữ phụ #586779, chính #356797, viền #e0e4e9. Segoe UI như MVC; tiêu đề 32px, đề mục 20px, nội dung 15px, phụ 12–14px.
- Chi tiết phòng: nút Đặt lịch xem phòng là hành động chính trong cột bên phải; Lưu phòng là phụ.
- Trang chọn lịch: trái là ngày/giờ và ghi chú; phải là căn phòng cố định cùng tóm tắt ngày/giờ đã chọn và nút gửi. Không phải chọn lại phòng. Hai cột căn cùng mép trên.
- Gửi xong mở chi tiết yêu cầu Chờ xác nhận. Menu Lịch hẹn mở danh sách, luôn giữ lịch vừa tạo và các yêu cầu hết hạn trong phiên thử. Chi tiết phòng và đặt lịch đánh dấu Tìm phòng; danh sách/chi tiết yêu cầu đánh dấu Lịch hẹn.
- Khung giờ xám có nhãn Đã có lịch/Đang giữ/Bạn đã có lịch. Khung dưới 6 giờ ẩn hoàn toàn. Không ép thời lượng xem.
- Có trạng thái ngày đã kín, chưa mở lịch, lỗi tải/thử lại, khung vừa bị đặt, lỗi gửi/thử lại. Ghi chú được giữ khi lỗi hoặc chuyển ngày. Lỗi thao tác dùng khối đỏ có nút đóng theo mẫu phản hồi Tenant/Admin.

## Hướng dẫn review

1. Bắt đầu ở chi tiết phòng, bấm **Đặt lịch xem phòng**. Phòng/giá/địa chỉ giữ nguyên.
2. Đồng hồ thử mặc định **07:00, 16/10/2026**. Không thấy 09:00/11:00; 13:00 đủ đúng 6 giờ nên chọn được. Chọn giờ, nhập ghi chú, kiểm tra tóm tắt bên phải rồi gửi.
3. Chi tiết mới ở Chờ xác nhận, giữ đến **09:00**. Bấm menu Lịch hẹn để xem lại. Lịch đã xác nhận có sẵn ở ngày 17 lúc 16:00 là dữ liệu minh họa và khung đó không chọn lại được.
4. Đổi đồng hồ thử sang 09:00 để xem hết hạn. Khung 13:00 được nhả nhưng vẫn ẩn vì còn dưới 6 giờ. Khung 17:00 của yêu cầu minh họa khác được nhả và còn đủ thời gian. Gửi mới lúc 09:00 thì deadline 11:00.
5. Chọn **Khung vừa có người đặt**, chọn một giờ rồi gửi. Nhận lỗi một lần, giờ đó bị khóa, giữ ghi chú và cho chọn giờ khác. Chọn **Lỗi gửi một lần** để thử gửi lại cùng giờ. Các lựa chọn ở dải vàng là công cụ demo, không phải thiết lập sản phẩm.
6. Thử ngày 18 (đã kín), ngày 19 (chưa mở), chuyển tuần, quay lại chi tiết phòng hoặc nút Back của trình duyệt. Nút Làm lại xóa dữ liệu thử trong phiên và khôi phục 07:00.

## Dữ liệu và giới hạn

HTML/JS → dữ liệu minh họa trong bộ nhớ của tab → hiển thị; không có API đặt lịch hoặc nguồn lịch thật. Đồng hồ thử là giờ Việt Nam cố định, điều chỉnh thủ công, không phải thời gian server. Hai tuần ngày mẫu chỉ phục vụ review, không xác lập giới hạn đặt trước cho nghiệp vụ. Tải lại trang mất yêu cầu thử. Nhãn người dẫn là chính chủ nhà trong ví dụ, chưa chốt ánh xạ dữ liệu chung.

Các liên kết Trang chủ/Tìm phòng/Đã lưu/chuông/tài khoản dẫn về localhost:8770 qua GET; rời bản thử sẽ về giao diện MVC hiện tại. Đặt lịch, gửi yêu cầu và Lịch hẹn trong bản thử ở riêng cổng 8766. Ảnh là ảnh thay thế trong repo, không phải ảnh phòng thật.

Chưa sửa controller, model, enum, DbContext, migration, Program.cs hay database nhóm. Chưa triển khai cơ chế cạnh tranh nhiều tài khoản, hạn giữ bền vững hoặc xử lý xác nhận Landlord. Bước ghép thật cần hợp đồng trong `tenant-appointment-data-contract.md`. Chưa thiết kế lại tablet/mobile; chỉ có fallback hẹp để trang không tràn.

## Xác minh bản thử

- Vòng đầu: 31 kiểm tra trình duyệt đạt, gồm luồng chi tiết → đặt → Pending → danh sách trên desktop 1024/1199/1440/1920; mốc 6 giờ, đúng hạn 2 giờ, giữ ghi chú và encode, khung vừa mất, lỗi gửi/thử lại, kín/chưa mở/lỗi tải, chuyển tuần, bàn phím, Back/Forward, sticky và fallback hẹp. Không có lỗi JS hay request ghi.
- Sau khi sửa căn cột và giữ lịch sử: 24 kiểm tra bổ sung đạt, gồm luồng ở bốn chiều rộng, hai yêu cầu cùng phiên vẫn giữ chi tiết/lịch sử riêng, deadline tính từ lúc gửi mới, hết hạn không sống lại khi lùi đồng hồ demo, Làm lại giữa lúc gửi không tạo yêu cầu muộn, fallback 320/390/834.
- Đã xem ảnh chi tiết phòng, chọn lịch 1440/1024 và Pending; kiểm tra lại ảnh 1440 sau căn cột. Kết quả và ảnh lưu tại thư mục artifact `tenant-booking-desktop` trong chat.
- Không chạy lại bộ C# vì thay đổi chỉ là bản thử HTML/CSS/JS riêng. Kết quả ứng dụng trước đó vẫn là 107/107; không gán kết quả đó cho tính năng đặt lịch thật.

Nhánh `feature/tenant-p3`, chưa commit/push. Bản thử chờ Dương review trước bước ghép dữ liệu/chức năng.
