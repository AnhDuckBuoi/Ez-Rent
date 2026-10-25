# Tự nhận tin và trạng thái đã xem

> Bổ sung sau lỗi đăng nhập khi review: preview đã giới hạn cơ chế tự vào Tenant, không can thiệp Login/Landlord hoặc ghi đè phiên đang đăng nhập. Bộ kiểm thử sau sửa là 283/283; 6 kiểm tra đăng nhập trình duyệt đạt. Xem phần đăng nhập hai vai trò trong [README kiểm thử](../tests/TenantSecurity.Tests/README.md). Dùng cửa sổ thường và ẩn danh để giữ hai tài khoản độc lập.

Ngày 25/10/2026. Dương duyệt tối ưu chat Tenant–Landlord theo hai hành vi: nhận tin tự động và hiển thị đã xem khi người nhận đang thực sự đọc. Giữ ASP.NET Core MVC, SignalR và bảng Conversation/Message hiện có; không thêm model, migration hoặc trạng thái online toàn hệ thống.

## Quy tắc hiển thị

- Hai bên mở đúng cuộc trò chuyện, tin mới tự xuất hiện qua thông báo SignalR và GET nội dung có kiểm tra thành viên. Không cần bấm tải hoặc F5.
- Tin gửi thành công hiện **Đã gửi**. Chỉ chuyển **Đã xem** sau khi phía nhận xác nhận những ID tin đã hiển thị trong vùng đọc bằng POST có CSRF. Không đánh dấu đã xem tại lúc gửi hoặc chỉ vì người nhận đang đăng nhập.
- Tab ẩn, cuộc trò chuyện khác, hộp thư mobile đang che khung chat, hộp thoại thông tin phòng đang mở hoặc tin nằm ngoài vùng cuộn đều chưa được tự tính đã xem. Khi quay lại/đóng hộp thoại/cuộn đến tin, ứng dụng kiểm tra lại. Debounce đọc 200 ms; thời gian thực tế còn tùy kết nối.
- Kết nối thành công bình thường ẩn nút **Tải tin mới**. Nút là đường khôi phục khi socket hoặc tải nội dung thất bại; form và đường tải vẫn dùng được khi tắt JavaScript.
- Tự đồng bộ ở lần kết nối đầu, sau kết nối lại và khi quay lại tab. Tín hiệu đến trong lúc tải/gửi tin hoặc mở hộp thoại được giữ lại, không phải chờ thêm một tin khác để kích hoạt.
- GET đồng bộ lỗi sẽ giữ giao diện/bản nháp và tự thử lại có khoảng nghỉ tăng dần, tối đa 15 giây. Request có hạn chờ. Gửi tin bị mất phản hồi không tự gửi lại để tránh bản sao; nội dung đang soạn được giữ để đối chiếu.
- Giữ bản nháp, vị trí con trỏ và vị trí cuộn. Người đang đọc lịch sử không bị tự kéo xuống; tin chưa lọt vào vùng nhìn thấy chưa được tính đã xem.

## Phạm vi mã

`chat-realtime.js` quản lý kết nối và trạng thái khôi phục, phát sự kiện khi có thay đổi hội thoại/kết nối thành công. `tenant-messages.js` và `landlord-chat.js` giữ cờ cần đồng bộ, gom sự kiện, tải nội dung và đánh dấu đúng tin đang nhìn thấy. View dùng nhãn **Đã xem**, cùng thuộc tính cho nút khôi phục. Khung `_Layout.cshtml`, dịch vụ gửi tin, kiểm tra quyền và cách lưu database giữ nguyên.

Không thêm đang nhập, online/offline theo người dùng, push notification, hàng đợi gửi offline, tệp đính kèm hoặc cơ chế chat nhiều máy chủ. Gói này không bổ sung màn Admin trả lời hỗ trợ.

## Kiểm tra và giới hạn

- Bộ HTTP/Razor/EF: **275/275 đạt**, không bỏ qua. Ca mới xác nhận GET hai phía không tự đánh dấu; người nhận POST đọc mới làm người gửi thấy **Đã xem**, cả hai chiều. Các ca giới hạn ID, CSRF, thành viên và dữ liệu riêng tiếp tục chạy.
- Trình duyệt với hai phiên Identity riêng và một người ngoài: **16 kiểm tra đạt**, không lỗi JavaScript chưa xử lý. Đã thử nhận tin/đã xem hai chiều không reload, giữ bản nháp, hội thoại khác, hộp thoại đang mở, GET lỗi, tín hiệu trong lúc GET chưa xong, tab ẩn (mô phỏng visibility), hộp thư mobile, mất mạng và lần kết nối đầu thất bại. Người ngoài không nhận sự kiện.
- Đã kiểm tra giao diện chat ở 1440×1000, 834×1112 và 390×844. Tin đến và phản hồi đã xem trong lần thử local cuối khoảng 0,5 giây; đây không phải cam kết độ trễ trên mọi mạng.
- Bằng chứng local ở `chat-live-qa` cạnh checkout tích hợp: `chat-live.trx`, `browser-results.json`, ảnh Desktop/Tablet/Mobile. SQLite và dữ liệu thử riêng; chưa ghi database nhóm hoặc xác nhận thiết bị vật lý/Safari.

## Dương và Đức tự thử

1. Chạy preview từ nhánh tích hợp theo `tests/TenantSecurity.Tests/README.md`, dùng cổng 8771. Mở `/Tenant/Dashboard/Messages?ConversationId=101&View=chat` với Tenant demo; dùng phiên trình duyệt riêng đăng nhập chủ nhà rồi mở `/Landlord/Messages?c=101`.
2. Hai bên để khung chat tại tin mới nhất. Gửi qua lại: bên nhận phải thấy tin, bên gửi phải thấy **Đã xem** mà không tải trang/bấm tải.
3. Tenant chuyển cuộc trò chuyện khác hoặc về hộp thư trên mobile. Chủ nhà gửi: vẫn **Đã gửi**. Tenant mở lại đúng hội thoại và nhìn thấy tin thì mới **Đã xem**.
4. Mở thông tin phòng, gửi từ phía kia rồi đóng thông tin: tin phải tự xuất hiện. Thử ngắt mạng và kết nối lại: tin bị lỡ phải được tự đồng bộ.

Bản sửa đang chờ Dương review trước khi tạo commit tiếp theo; không gộp vào main.
