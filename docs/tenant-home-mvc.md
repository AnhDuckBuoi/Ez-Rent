# Trang chủ Tenant MVC — gói đã được Dương đồng ý

> Cập nhật 16/10/2026: đã đồng bộ khung desktop từ 1024px, phân trang số và phản hồi theo mẫu Admin. Xem [tenant-desktop-shell.md](tenant-desktop-shell.md). Bộ kiểm tra hiện tại: 107/107 đạt.


Ngày 15/10/2026. Nhánh `feature/tenant-p3`. Căn cứ: Dương duyệt kế hoạch đưa Home v3 vào MVC, cá nhân hóa tên/lịch/thông báo/phòng đã lưu, giữ hướng dẫn cố định, kiểm tra quyền và responsive. Không tự commit/push.

Cập nhật review 16/10/2026: Dương chưa ưng phần thị giác và yêu cầu thử skill khác. Đã dựng [đề xuất v4 bằng Frontend Design](tenant-v4-design-notes.md), sáng/hiện đại/ảnh chụp theo lựa chọn của Dương. MVC dưới đây vẫn giữ nguyên để so sánh, chờ review bản thử mới.

## Kết quả và cách review

Mở `http://127.0.0.1:8770/Tenant/Dashboard`. Đây là **Razor/MVC thực tế**, chạy bằng test host riêng với SQLite trong bộ nhớ và danh tính mẫu Minh Anh, không phải HTML outline. Banner vàng phân biệt dữ liệu mẫu. Không chạy Program.cs chính hoặc kết nối/ghi database nhóm. Bản outline vẫn ở cổng 8766.

1. Kiểm tra lời chào, lịch đã xác nhận, hai thông báo và tối đa hai phòng đã lưu.
2. Mở lịch và phòng; từ phòng quay lại đúng Trang chủ. Tin hết công khai được che nội dung và dẫn về danh sách đã lưu.
3. Mở thông báo: đánh dấu đã đọc bằng POST, mở đúng trang liên quan khi được phép. Chuông dẫn đến danh sách phân trang; có nút đánh dấu tất cả đã đọc. Lượt GET không tự thay đổi trạng thái.
4. Chọn chủ đề hướng dẫn; mở/thu gọn nội dung, thử checklist. Đổi kích thước vẫn giữ trạng thái trong trang. Nội dung đọc được khi tắt JavaScript.
5. Mở Hỗ trợ/Tin nhắn. Hỗ trợ có hướng dẫn và liên kết yêu cầu sửa chữa; trò chuyện và gửi hỗ trợ/phản ánh vẫn có nhãn đang hoàn thiện, không giả vờ rằng UC09/UC10 đã chạy.

Test host giới hạn các route đã cho phép. Quản lý tài khoản và các module ngoài danh sách vẫn dùng trang giải thích của test host; ứng dụng chính sử dụng route Identity/module sẵn có. Tải lại tiến trình test host khôi phục dữ liệu mẫu.

## Luồng dữ liệu

HTTP GET → DashboardController xác định CurrentUserId → TenantHomeReader truy vấn EF có điều kiện theo người đó → TenantHomeModel → Razor Index và layout riêng → HTML. Không truy vấn database trong View, không lấy người dùng qua tham số URL/form. Các query chạy lần lượt trên một DbContext; không thực hiện truy vấn song song trên cùng context.

- FullName từ AspNetUsers; thiếu tên dùng “bạn”, không tự lấy email làm tên hiển thị.
- Lịch tiếp theo chỉ gồm Confirmed còn ở tương lai, thứ tự ViewingDate rồi ID. Pending chỉ là số yêu cầu đang chờ. Dùng AppointmentModel.From để che tin không còn công khai như màn lịch hiện có.
- Favorite lọc UserId, đếm toàn bộ để khớp danh sách; lấy hai mục mới nhất. Dùng chung quy tắc trình bày tin hết khả dụng của TenantRentalDiscovery.
- Notification lọc UserId; hai mục gần nhất trên Home, danh sách riêng mười mục/trang, thứ tự CreatedAt rồi ID. Nội dung/tên luôn được Razor encode.

## Thông báo và quyền truy cập

NotificationController kế thừa guard TenantControllerBase, yêu cầu role Tenant và định danh. Details/Open chỉ lấy thông báo của người đăng nhập. Open và ReadAll là POST có ValidateAntiForgeryToken. Update chỉ tác động IsRead, luôn giữ điều kiện UserId và không xóa lịch sử; gửi lại vẫn an toàn. Redirect không nhận địa chỉ tùy ý từ trình duyệt.

Link trong database là dữ liệu chưa tin cậy. Chỉ chấp nhận định dạng chính xác `/Tenant/{ViewingAppointment|RentalPost|Contract|Invoice|RepairRequest}/Details/{id}`. ID phải là số nguyên dương hợp lệ. Trước khi chuyển trang, kiểm tra thêm quyền sở hữu hoặc tin công khai tại đích. Link ngoài, protocol-relative, Landlord, hành động ghi, encoded ID hoặc query tùy ý đều không được theo; thay vào đó mở nội dung thông báo của chính người đó. Các controller đích tiếp tục kiểm tra quyền độc lập.

Không sửa hoặc tuyên bố đã xử lý các lỗ phân quyền Landlord/global đã ghi ở tenant-security-review.md.

## Thời gian và nguồn nội dung

Ngày hiển thị Home lấy giờ hiện tại ở UTC+7. Notification.CreatedAt có quy ước ghi UTC trong model, nên chuyển sang giờ Việt Nam khi trình bày. ViewingDate cũ từ datetime-local là wall time: giữ nguyên giờ đã lưu/hiển thị và so với wall time Việt Nam để chọn lịch tương lai, không chuyển hoặc ghi lại dữ liệu cũ. Dữ liệu legacy từng được nhập bằng UTC không có dấu phân biệt vẫn cần nhóm chốt trong hợp đồng dữ liệu lịch; gói này không giải quyết bằng migration âm thầm.

Home đọc bảng Notification sẵn có. Chưa ghép **phát sinh** thông báo từ thao tác Landlord hoặc tự tạo thông báo khi tải trang. Database chưa có thông báo thì hiển thị trạng thái rỗng. Seed minh họa chỉ nằm trong test executable.

Góc người thuê là hướng dẫn cố định mang nhãn “Hướng dẫn EZ Rent”; không giả tin mới, ngày xuất bản hoặc CMS. Tạo/quản lý bài viết chờ chốt nguồn với Đức. Không thay đổi model, enum, DbContext, migration hay Program.cs; không thực hiện ghi vào database nhóm.

## Kiểm tra và review giao diện

- Build thành công. 16 cảnh báo nullable có sẵn ở EmailSender và view ngoài Tenant; không có cảnh báo mới trong phần Home.
- **102/102 kiểm thử** đạt. Thêm kiểm tra Tenant/missing identity, dữ liệu riêng, lựa chọn lịch xác nhận theo mốc cố định, che tin nháp, XSS, phân trang thông báo, CSRF, GET không ghi, owner-scoped/idempotent read-all, giả tham số ID/redirect và liên kết sai quyền/ngoài hệ thống.
- Chromium kiểm tra năm trang Home/Notification/Details/Help/Messages tại tám chiều rộng 320–1440px: 40 tổ hợp không tràn ngang/ID trùng/lỗi JavaScript với dữ liệu mẫu.
- Thử bộ lọc hướng dẫn, details/checklist, đổi breakpoint giữ trạng thái và thứ tự đọc, mở lịch/phòng/quay lại Home, POST đọc một/tất cả thông báo, lối vào Help/Messages.
- Lượt kiểm tra ảnh đầu tìm được tiêu đề dài không có khoảng trắng làm tràn và panel tablet chưa chiếm đủ chiều ngang do display:contents. Đã sửa một đợt, xác nhận lại tám chiều rộng kể cả tiêu đề dài, chiều rộng panel, trạng thái checkbox/details và reduced motion. Đã xem ảnh cuối mobile/tablet/desktop.
- Đây là kiểm tra triển khai và review trực tiếp theo Impeccable, không phải chứng nhận accessibility toàn hệ thống hoặc gate tự động. Token/nhận diện v3 được giữ; không có thay thương hiệu hay tài nguyên ảnh mới.

Kết quả kiểm thử nằm trong tests/TenantSecurity.Tests/TestResults (được Git bỏ qua). Ảnh review nằm ngoài repository, thư mục visualization `tenant-home-mvc`.

## Phạm vi còn lại

Chưa ghép backend khung giờ 6h/giữ 2h/hết hạn, trò chuyện và gửi hỗ trợ/phản ánh, CMS, responsive các module Tenant khác, hoặc nguồn tạo thông báo ở Landlord. Gói tiếp theo cần trình kế hoạch riêng theo yêu cầu của Dương.

## Review bổ sung: ghim thanh điều hướng

Theo nhận xét trực tiếp của Dương trên header, đã dùng sticky top:0 cho thanh trên; nội dung tiếp tục cuộn bên dưới, banner dữ liệu mẫu tự cuộn ra ngoài. Kiểm tra cuộn ở viewport 938×930 của nhận xét, desktop 1440 và mobile 390: header đứng ở y=0, chuông vẫn mở được và nội dung đích khi cuộn không bị che. Bổ sung scroll-padding-top theo chiều cao header. Không đổi backend hoặc khởi tạo lại dữ liệu mẫu trong lần sửa CSS này.
