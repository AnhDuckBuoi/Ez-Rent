# Outline tablet và desktop Tenant — bản 03

Ngày: 15/10/2026. Nhánh `feature/tenant-p3`. Dương yêu cầu tự kiểm tra bản đặt lịch rồi tiếp tục và làm rõ phạm vi mobile/desktop. Trước khi thực hiện, đã trình bước tiếp theo là dựng outline màn hình lớn để review.

Cập nhật Home bản 03 theo Impeccable: có **Trang chủ riêng** mở mặc định, ưu tiên thông báo/bài viết/lịch, tối đa hai phòng đã lưu ở cuối. Tìm phòng giữ trang danh sách riêng. Xem [phạm vi, nguồn dữ liệu và kiểm tra Home](tenant-home-plan.md). Bản review hiện có 5 màn; các ghi nhận 4 màn bên dưới thuộc đợt kiểm tra bản 01.

## Hiện trạng được phân biệt rõ

- Các màn Tenant đã triển khai ưu tiên mobile theo P3, có CSS thích nghi cơ bản. Kiểm tra không tràn ngang ở màn hình rộng chưa đồng nghĩa đã có thiết kế desktop riêng được nghiệm thu.
- `tenant-appointment-preview.html?v=2` là bản xem thử nghiệp vụ mobile, hình điện thoại cố định. Các bảng điều khiển bên ngoài không phải giao diện sản phẩm desktop.
- Gói này tạo outline tablet/desktop để Dương review trước khi áp vào ứng dụng thật. Không sửa controller/view/CSS sản phẩm, Figma, model hoặc database trong bước outline. Các mô phỏng chỉ tồn tại trong trang, tải lại khôi phục mẫu.

## Bố cục đề xuất

| Màn | Desktop từ 1200 px | Tablet 768–1199 px | Mobile dưới 768 px |
|---|---|---|---|
| Điều hướng | Header có Trang chủ/Tìm phòng/Lịch hẹn/Đã lưu và chuông; vùng tài khoản | Header gọn, bỏ mục chưa có chức năng | Header có lối vào lịch/yêu thích và chuông, giữ 5 mục P3 phía dưới |
| Trang chủ | Lịch xác nhận/thông báo/bài viết và cột tiện ích nhỏ; tối đa hai phòng đã lưu | Home có cột tiện ích từ 1000px, dưới mốc này một luồng đọc | Lịch xác nhận → thông báo → lối tắt → bài đọc inline → phòng đã lưu |
| Tìm phòng | Bộ lọc trái 240 px; kết quả 3 cột | Mở bộ lọc qua dialog; kết quả 2 cột | Bộ lọc dialog; kết quả 1 cột |
| Chi tiết phòng | Gallery 2 ảnh; nội dung và chủ nhà 2 cột, bảng chủ nhà bám khi cuộn | Gallery 1 ảnh lớn, nội dung và chủ nhà 2 cột | Xếp dọc; nút đặt lịch cố định phía trên điều hướng dưới |
| Chọn giờ | Chọn ngày/giờ và tóm tắt yêu cầu cạnh nhau | 2 cột khi đủ chỗ, tóm tắt gọn | 1 cột; tóm tắt dưới vùng chọn, nội dung được phép cuộn |
| Lịch hẹn | Danh sách thẻ bên trái, chi tiết bên phải; thẻ có hành động ở đáy | 2 cột thẻ; mở chi tiết thành màn riêng có nút quay lại | 1 cột thẻ; mở chi tiết riêng có nút quay lại |

Mốc 768/1200 px là đề xuất để review, không gắn cứng vào tên thiết bị. Trang review có khung 390/834/1024/1440 px; iframe có chiều rộng thật theo preset, được thu nhỏ bằng transform để vừa bảng review. Thay đổi thiết bị không xóa trạng thái đang thao tác. Mở riêng canvas để kéo kích thước cửa sổ thực tế.

## Bản để review

- Bảng so thiết bị: `http://127.0.0.1:8766/tenant-responsive-outline.html`.
- Canvas trực tiếp: `http://127.0.0.1:8766/tenant-responsive-canvas.html?view=search` (các màn khác: `detail`, `booking`, `appointments`).
- Bản nghiệp vụ mobile đã chốt: `http://127.0.0.1:8766/tenant-appointment-preview.html?v=2`.

Tất cả ảnh phòng là SVG minh họa tạo trong code; không tải ảnh/font/thư viện từ mạng. Dữ liệu và thao tác đều có nhãn minh họa ở bảng review. Trang chọn giờ trong outline phục vụ bố trí không gian và tóm tắt cạnh nhau; chưa phải toàn bộ bước kiểm tra/gửi/lỗi/hết hạn của bản nghiệp vụ mobile.

## Cách review

1. Giữ Desktop 1440, thử cả 4 màn. Ở tìm phòng, áp bộ lọc, sắp xếp, sang trang; mở chi tiết rồi quay lại phải giữ điều kiện và trang. Thử lưu/bỏ lưu và mục Đã lưu.
2. Chuyển Tablet 834/1024. Bộ lọc chuyển vào bảng mở; áp dụng hoặc Esc đóng bảng, bàn phím quay về nút mở. Danh sách còn 2 cột.
3. Vào Lịch hẹn, bấm Xem chi tiết của hai lịch khác nhau. Desktop đổi chi tiết ở bên phải, không rời danh sách; tablet/mobile mở chi tiết riêng và có nút về danh sách. Nút Xem chi tiết của các thẻ cùng hàng phải thẳng nhau, dù ghi chú/phản hồi khác nhau.
4. Vào lịch hết hạn → Chọn lịch mới: thông tin phòng phải khớp lịch vừa xem. Chọn ngày/giờ, nhập ghi chú; đổi ngày giữ ghi chú. Gửi chỉ tạo Pending minh họa trong trang, hiển thị hạn 09:00 do đồng hồ mẫu 07:00 cố định.
5. Chọn Mobile 390 để đối chiếu; phần triển khai mobile đang chạy vẫn giữ nguyên. Sau khi Dương duyệt outline mới có gói áp bố cục desktop/tablet vào view thật.

## Kiểm tra trước khi dựng outline

- Chạy lại 84/84 kiểm thử Tenant, tất cả đạt; dùng database SQLite riêng trong bộ nhớ, không thực thi `Program.cs` ứng dụng hoặc SQL Server nhóm. Kết quả `tenant-responsive-review.trx` nằm trong thư mục kết quả bị Git bỏ qua.
- Chromium kiểm tra lại bản nghiệp vụ mobile: ranh giới 6 giờ, giữ riêng, trước/đúng deadline 2 giờ, xác nhận không hết hạn, từ chối/hủy nhả khung, lỗi gửi và khung vừa mất giữ ghi chú, encode văn bản và thời gian dưới 6 giờ sau khi nhả. Kiểm tra màn lịch hẹn MVC và hành động thẳng hàng. Không có lỗi JavaScript trong các luồng đã thử.

## Kiểm tra outline

Đã kiểm tra 4 màn tại 320/390/768/834/1024/1199/1200/1440 px không tràn ngang và không có lỗi JavaScript. Đã xem ảnh desktop tìm phòng/lịch hẹn, tablet chi tiết và mobile chọn giờ.

Các luồng tương tác đã kiểm tra: nhãn bộ lọc khớp giá trị, khoảng giá sai bị chặn, kết hợp điều kiện, sắp xếp/phân trang, lưu/bỏ lưu, gallery, quay lại giữ trang, từ khóa/ghi chú encode, dialog tablet và phục hồi focus, danh sách/chi tiết theo thiết bị, hành động thẳng hàng, chọn lịch mới đúng phòng, đổi ngày giữ ghi chú, mốc bị giữ riêng và deadline mẫu. Trong lúc kiểm tra đã sửa lỗi markup option và điều hướng mặc định làm mất trạng thái quay lại.

Bảng review được kiểm tra thêm: iframe đúng chiều rộng preset, đổi thiết bị giữ trạng thái, chuyển màn bên trong cập nhật tab/ghi chú bên ngoài, bảng review không tràn ngang tại 320/390/768/1280/1600 px, không có ID trùng trong 4 màn và hình minh họa lấp vùng ảnh thẻ. Đã cập nhật ảnh desktop sau khi sửa bộ lọc.

Chưa thay đổi giao diện ứng dụng thật hoặc nhận định luồng slot backend đã hoàn tất. Nguồn lịch/giữ chỗ/expired vẫn theo `tenant-appointment-data-contract.md` để Dương tự ghép hai phía. Chưa commit/push.
