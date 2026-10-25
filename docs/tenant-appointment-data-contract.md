# Bàn giao nguồn lịch Tenant–Landlord — gói 3C

> Cập nhật 16/10/2026: Dương giữ phạm vi trợ lý ở giao diện Tenant và tự phối hợp dữ liệu/Landlord. Màn MVC và điểm nối đọc lịch đã có; chưa đổi model/schema hoặc gửi lịch thật. Xem [tenant-booking-mvc-ui.md](tenant-booking-mvc-ui.md).


Quy tắc nghiệp vụ dưới đây đã được Dương chốt qua review. Cấu trúc dữ liệu và API là đề xuất để Dương tự phối hợp/ghép hai phía với Đức; trợ lý tiếp tục phạm vi Tenant. Chưa sửa model dùng chung, enum, DbContext hoặc migration; chưa ghi SQL Server nhóm.

## Quy tắc đã chốt

- Tenant chọn ngày và **giờ đến xem** từ các mốc chủ nhà đã mở. Không nhập giờ tùy ý. Thời lượng xem do hai bên tự sắp xếp; không áp mặc định 30 phút hoặc 15 phút nghỉ.
- Khi gửi, giờ hẹn phải cách thời gian server ít nhất 6 giờ; đúng 6 giờ được chọn. Ẩn mốc chưa đủ 6 giờ, kiểm tra lại ở server.
- Gửi thành công tạo `Pending` và giữ riêng mốc đó cho một yêu cầu. Khóa theo **người dẫn + thời điểm**, tính cả các phòng khác, không chỉ theo ID phòng hoặc ID slot riêng lẻ.
- Chủ nhà có **2 giờ kể từ lúc gửi** để phản hồi. Server cấp/lưu `ResponseDeadline = SubmittedAt + 2 giờ`. Khi `now >= ResponseDeadline`, yêu cầu chưa xác nhận hết hạn và không còn giữ mốc. Xác nhận chỉ thành công khi còn `Pending` và `now < ResponseDeadline`.
- Xác nhận trước hạn tiếp tục giữ lịch; từ chối, Tenant hủy hoặc hết hạn thì nhả. Yêu cầu cũ và phản hồi được giữ lại. Chưa tự thêm quy tắc hủy sát giờ; cutoff hủy cần review riêng.
- Nhả khung không đồng nghĩa luôn đặt lại được. Ví dụ gửi lúc 07:00 cho lịch 13:00, hết hạn lúc 09:00: lịch được nhả nhưng 13:00 không còn đủ 6 giờ nên vẫn ẩn với yêu cầu mới. Không tự miễn quy tắc báo trước.

**Giới hạn của lịch chỉ có giờ đến:** hệ thống chặn nhiều người giữ cùng mốc của cùng người dẫn, chưa thể bảo đảm hai lần xem không chồng thời lượng thực tế. Chủ nhà cần mở các mốc đủ cách nhau. Nếu sau này cần bảo đảm khoảng thời gian không giao nhau, phải bổ sung khoảng bận/nghỉ do chủ nhà cấu hình và review riêng.

## Source còn thiếu gì

`ViewingAppointment` hiện có phòng, Tenant, `ViewingDate`, ghi chú hai bên, trạng thái và timestamps; chưa có nguồn mốc chủ nhà mở, liên kết mốc, người dẫn tại lúc đặt hoặc hạn phản hồi. Enum hiện chỉ có `Pending`, `Confirmed`, `Completed`, `Cancelled`, `Rejected`. Cần thống nhất biểu diễn **hết hạn phản hồi**, không dùng `Rejected` để giả làm chủ nhà đã từ chối. Preview có nhãn hết hạn để review UX; nhãn đó chưa phải enum đã thêm trong ứng dụng.

Controller lịch hẹn Landlord hiện là CRUD scaffold chưa giới hạn role/owner và vẫn xóa bản ghi. Phần tích hợp cần sửa các đường này, không chỉ thêm màn mở lịch. Các vấn đề tài chính ngoài Tenant vẫn theo `tenant-security-review.md`.

Form cũ bind `datetime-local` rồi lưu `DateTime` trực tiếp; timestamps dùng UTC và SQL không giữ `DateTime.Kind`. Không tự cộng 7 giờ cho lịch cũ. Đề xuất nguồn mới trả thời gian có offset, lưu UTC, hiển thị Việt Nam; phải chốt cách phân loại lịch cũ trước khi đổi quy ước lưu.

## Dữ liệu tối thiểu để Dương phối hợp

| Thành phần | Dữ liệu cần có |
|---|---|
| Mốc chủ nhà mở | ID, phòng được phép xem, người dẫn, giờ đến có múi giờ, mở/đóng, phiên bản. Người dẫn có thể là chính Landlord nếu nhóm chọn cách ánh xạ đó. Không bắt buộc giờ kết thúc. |
| Yêu cầu xem | ID mốc, Tenant từ đăng nhập, người dẫn và giờ hẹn tại lúc gửi, thời điểm gửi, hạn phản hồi, trạng thái, ghi chú, phiên bản/khóa chống gửi lặp. Không dời lịch đã xác nhận âm thầm khi sửa mốc hoặc chủ phòng. |
| Giữ mốc | Chỉ một giữ chỗ còn hiệu lực theo người dẫn + giờ đến, kể cả mốc công bố ở phòng khác. Hạn giữ lấy từ yêu cầu; xác nhận tiếp tục chiếm mốc, kết thúc/hủy/từ chối/hết hạn giải phóng theo trạng thái. |

Đây là yêu cầu dữ liệu, chưa áp đặt tên bảng/cột. Phần tích hợp chọn entity/index/migration và phương án tương thích dữ liệu cũ.

## Thao tác đọc/ghi cần thống nhất

1. **Đọc theo ngày:** nhận ID phòng và ngày Việt Nam, giới hạn khoảng ngày phía server. Chỉ đọc tin Approved + Available. Trả thời gian server, các ID mốc cùng giờ đến có offset và trạng thái chọn được/đang giữ/đã có lịch; ngày chưa mở khác với ngày hết mốc phù hợp. Không trả Tenant hoặc ghi chú của người khác. Chỉ hiển thị hạn giữ của chính Tenant nếu cần; không tiết lộ dữ liệu người giữ khác.
2. **Gửi yêu cầu:** Tenant gửi ID mốc, ghi chú tối đa 500 ký tự, khóa chống gửi lặp và CSRF. Server tự lấy Tenant/phòng/người dẫn/giờ; kiểm tra tin, mở/đóng, giới hạn 6 giờ và giữ chỗ theo người dẫn. Lấy mốc và tạo `Pending` kèm hạn giữ trong cùng transaction. Hai yêu cầu cạnh tranh chỉ một thành công; yêu cầu còn lại nhận lỗi khung vừa mất và giữ ghi chú để chọn lại.
3. **Xác nhận:** role Landlord và đúng owner/người phụ trách; kiểm tra trạng thái, phiên bản và hạn ở server. Xác nhận với nhả khung/hết hạn phải được điều phối nguyên tử, không có trường hợp lịch đã nhả cho người khác rồi chủ nhà vẫn xác nhận yêu cầu cũ.
4. **Từ chối/hủy/hết hạn:** giữ bản ghi và phản hồi; giải phóng mốc. Hết hạn cần cơ chế server bền vững để cập nhật trạng thái, đồng thời mọi đường đọc/giữ/xác nhận đều nhận biết hạn đã qua dù tiến trình nền chạy trễ. Timer trình duyệt chỉ để trình bày.

Ví dụ giờ đến `2026-10-20T14:00:00+07:00` tương ứng `07:00:00Z`. Server nhận gửi lúc `01:00:00Z` thì đủ 6 giờ, hạn phản hồi là `03:00:00Z`. Xác nhận đúng `03:00:00Z` đã hết hạn. Đọc lại mốc phải áp dụng cả hạn giữ lẫn giới hạn báo trước, không chỉ dựa vào nhãn cũ trên giao diện.

## Thứ tự ghép Tenant

1. Dương thống nhất nguồn mốc, trường deadline, biểu diễn hết hạn và giờ có offset với phía Landlord/Đức. Trợ lý không tự sửa phần Landlord theo câu trả lời phân công hiện tại.
2. Ghép chi tiết phòng → chọn ngày/giờ đến → kiểm tra → gửi `Pending` → chi tiết. Hiển thị “Giữ khung đến …”, “Chưa xác nhận, vui lòng chưa đến xem”; hết hạn hiển thị riêng và dẫn chọn lịch mới. Chưa cần đếm ngược từng giây.
3. Thay hoặc chặn cả Create và Edit cũ nhận giờ tự do, để không có đường ghi bỏ qua nguồn mốc, 6 giờ hoặc giữ riêng. Chưa bật đặt lịch thật khi chưa có nguồn được thống nhất.
4. Kiểm thử quyền, thời gian và cạnh tranh bằng dữ liệu riêng, sau đó Dương review. Migration/database nhóm vẫn là bước phối hợp riêng.

## Điểm nghiệm thu

- Đúng 6 giờ được gửi; thiếu 1 giây bị chặn. Mốc dưới 6 giờ ẩn, cả khi vừa được nhả.
- Cùng người dẫn và giờ đến ở hai phòng không thể có hai giữ chỗ còn hiệu lực. Hai Tenant cạnh tranh chỉ một nhận `Pending`; gửi lại cùng yêu cầu không tạo thêm giữ chỗ.
- Đúng deadline hết hạn; trước deadline xác nhận được; sau deadline không xác nhận lại được. Tiến trình nền chạy trễ không kéo dài hạn giữ.
- Xác nhận cạnh tranh với hết hạn/hủy/đặt lại không tạo hai lịch hoặc giải phóng nhầm mốc đã giữ bởi yêu cầu mới.
- Từ chối/hủy/hết hạn giữ lịch sử và phản hồi. Lịch `Confirmed` không hết hạn theo deadline phản hồi cũ.
- Giữ/nhả xong không bỏ qua trạng thái tin, giờ server hoặc quyền sở hữu. Tenant khác không đọc/hủy; Landlord khác không xác nhận.
- Ghi chú được encode và giữ khi lỗi gửi/khung vừa mất. Lịch cũ không bị chuyển giờ âm thầm.

## Preview theo quyết định mới

`tenant-appointment-preview.html?v=2` là HTML độc lập dùng dữ liệu mẫu và đồng hồ điều khiển, chưa kết nối ứng dụng hoặc database. Đã bỏ giờ kết thúc cố định, mô phỏng giữ riêng 2 giờ, hết hạn và nhả mốc; có thao tác demo ngoài điện thoại để thử xác nhận/từ chối/hủy. Trạng thái preview không tự thêm vào enum ứng dụng. Hướng dẫn và kết quả kiểm tra nằm trong `tenant-appointment-decisions.md`.
