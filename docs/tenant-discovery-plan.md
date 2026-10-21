# Gói 3A — Tìm phòng, chi tiết và yêu thích

Ngày lập và triển khai: 15/10/2026. Nhánh: `feature/tenant-p3`. Dương đã duyệt phạm vi gói 3A. Đã triển khai chức năng và kiểm tra; chờ Dương review giao diện chạy thử.

## Mục tiêu và căn cứ

Tenant tìm được phòng còn trống, xem đầy đủ thông tin công khai và lưu/bỏ lưu phòng ngay trong luồng tìm kiếm. Dùng dữ liệu thật qua EF Core, giao diện mobile bám P3; dữ liệu minh họa chỉ dùng khi kiểm thử, không thêm vào database nhóm.

P3 đã đối chiếu ở các node `360:724` (Tìm kiếm phòng), `360:817` và `360:917` (Chi tiết căn phòng). Gói này dùng bản đối chiếu nghiệp vụ đã có; chưa coi đó là nghiệm thu font, khoảng cách hoặc toàn bộ tương tác thiết kế.

Source hiện tại: `Controllers/RentalPostController.cs` là CRUD dùng chung, lấy mọi tin và có cả thao tác quản trị. `Areas/Tenant/Controllers/HomeController.cs` hiện chuyển đến Dashboard. `RentalPost`, `RoomImage`, `Service`, `Favorite` và Identity đã có dữ liệu nền; chưa có luồng khám phá dành riêng cho Tenant.

## Phạm vi đề xuất

| Màn | Nội dung và thao tác |
|---|---|
| Tìm phòng | Ô từ khóa; mở bộ lọc; số kết quả; thẻ ảnh, tiêu đề, địa chỉ, giá/tháng, diện tích, loại phòng và nút lưu; phân trang. |
| Chi tiết phòng | Gallery ảnh; tiêu đề, giá, diện tích và loại phòng; địa chỉ; mô tả; điện/nước/phí dịch vụ; tiện ích nếu đọc được dữ liệu hợp lệ; tên/ảnh chủ nhà; nút lưu phòng. |
| Yêu thích | Thẻ phòng đã lưu, vào chi tiết hoặc bỏ lưu; danh sách trống có lối đến Tìm phòng. Tin không còn công khai/khả dụng không được mở chi tiết qua danh sách đã lưu; vẫn cho bỏ lưu bản ghi của mình. |
| Không có kết quả | Giữ bộ lọc, giải thích không tìm thấy và có nút xóa bộ lọc. |
| Tin không còn khả dụng | Thông báo phòng không còn được xem trong luồng tìm kiếm, có lối quay lại danh sách; không lộ nội dung tin nháp/ẩn qua ID hoặc yêu thích. |
| Lỗi lưu/bỏ lưu | Giữ trang và bộ lọc; không báo thành công khi server chưa ghi nhận, có thể thử lại. |

Nút đặt lịch ở bản chức năng chỉ được nối đến luồng chọn khung thật sau khi thống nhất nguồn dữ liệu Landlord. Trong thời gian đó, cần thể hiện rõ chức năng chưa sẵn sàng, không đưa người dùng vào form nhập giờ tự do hoặc ghi yêu cầu từ khung mẫu. Chat cũng theo gói đã thống nhất riêng, không dựng nút gửi tin giả trong ứng dụng thật.

## Bộ lọc và cách hiển thị đề xuất

- Từ khóa: tiêu đề và địa chỉ.
- Khu vực: tỉnh/thành phố và quận/huyện lấy từ các tin công khai hợp lệ đang có. Không giả định dữ liệu địa chỉ là danh mục hành chính đã chuẩn hóa.
- Giá từ/đến theo đồng/tháng; diện tích từ/đến theo m²; kiểm tra số âm và khoảng nhập đảo ngược.
- Loại phòng theo enum hiện tại: Phòng trọ, Căn hộ, Nhà nguyên căn, Ký túc xá. Không thêm loại mới hoặc thay enum trong gói này.
- Sắp xếp: mới nhất, giá tăng/giảm, diện tích tăng/giảm. Thứ tự ổn định bằng ID khi bằng giá trị.
- Đề xuất 12 tin/trang; chuyển trang và quay lại danh sách giữ bộ lọc trên URL. Kết quả lọc cập nhật bằng Fetch/partial khi JavaScript có sẵn, form GET và link phân trang vẫn hoạt động khi JavaScript tắt. Chỉ giữ kết quả của thao tác lọc mới nhất khi nhiều yêu cầu trả về không cùng thứ tự.

Tiện ích đang lưu dưới `AmenitiesJson`; source chưa có quy ước nhập/đọc JSON nhất quán. Gói đầu đề xuất hiển thị dữ liệu hợp lệ với fallback an toàn và chưa bật bộ lọc tiện ích cho đến khi nhóm thống nhất định dạng. Không dùng tìm chuỗi con trong JSON để giả lập bộ lọc chính xác. Nếu Dương yêu cầu bộ lọc tiện ích ngay trong gói 3A, cần chốt thêm hợp đồng dữ liệu với Landlord/Đức trước.

## Quyền, dữ liệu và cách ghép vào ứng dụng

- Danh sách, số kết quả, danh mục bộ lọc và chi tiết cùng áp dụng `ApprovalStatus.Approved` và `RoomStatus.Available`.
- Dự kiến thêm controller/view ở `/Tenant/RentalPost`, dùng cùng yêu cầu role Tenant và Identity ID như gói bảo vệ dữ liệu. Chỉ có action xem; không có CRUD tin đăng trong controller này.
- Yêu thích dùng thao tác POST đặt trạng thái mong muốn (lưu hoặc bỏ lưu), có CSRF; gửi lặp không đảo trạng thái ngoài ý muốn. User lấy từ Identity. Server kiểm tra tin còn hợp lệ trước khi thêm lưu; bỏ lưu chỉ tác động bản ghi của chính user.
- ViewModel riêng cho bộ lọc, thẻ phòng và chi tiết. Chỉ đưa dữ liệu công khai cần dùng ra view; không đưa ID Identity, email đăng nhập hoặc nội dung kiểm duyệt nội bộ ra màn chi tiết.
- Ảnh không có/lỗi có hình thay thế; mô tả được Razor encode. Media/link đầu vào được kiểm tra trước khi dùng làm nguồn hiển thị. Phí `ServiceFee` và danh sách `Service` hiển thị theo dữ liệu đang lưu, không tự cộng lại thành phí mới vì nhóm chưa chốt ý nghĩa cộng gộp.
- Cập nhật lối vào Khám phá/Tìm phòng từ Tenant và màn yêu thích. Nếu cần chỉnh link trong layout chung, chỉ điều hướng người dùng Tenant sang route mới; không sửa chức năng các vai trò khác.

Luồng: request GET mang bộ lọc → xác thực Tenant → kiểm tra input → truy vấn tin hợp lệ/đếm/phân trang và trạng thái lưu của user → ViewModel → Razor/partial → kết quả. Khi lưu: POST có CSRF → kiểm tra user/tin → cập nhật Favorite → phản hồi để giao diện cập nhật hoặc báo lỗi.

## Những file dự kiến thay đổi

- Controller mới và helper truy vấn trong `Areas/Tenant`.
- ViewModel và view danh sách/chi tiết mới trong `Areas/Tenant`.
- Controller/view Favorite và lối vào từ Dashboard; phần link layout chung nếu cần.
- CSS/JavaScript riêng cho khám phá trong `wwwroot`, giữ cấu trúc MVC.
- Project kiểm thử hiện có để bổ sung kiểm tra truy vấn, quyền và POST yêu thích.

Không đổi model, enum, DbContext, migration, database nhóm hoặc nội dung có sẵn trong `Program.cs`. Không tự sửa controller công khai/Landlord/Admin trong gói này; các lỗ hổng đã ghi ở `tenant-security-review.md` vẫn là phần cần phối hợp, nên gói này không hoàn thiện phân quyền toàn ứng dụng.

## Điều kiện hoàn tất

1. Build thành công; các kiểm thử bảo vệ dữ liệu đã có tiếp tục đạt.
2. Tin nháp/ẩn/đã thuê bị loại ở danh sách, chi tiết, danh mục bộ lọc và action thêm yêu thích; Tenant A không sửa trạng thái lưu của B.
3. Lọc/sắp xếp/phân trang chính xác, giữ tham số; input sai có lỗi rõ ràng và không gây lỗi server. Kiểm tra cả kết quả trả về chậm khi lọc liên tiếp.
4. Lưu/bỏ lưu từ danh sách và chi tiết hoạt động, chống gửi lặp, giữ CSRF và xử lý trường hợp tin vừa mất khả dụng.
5. Kiểm tra ảnh thiếu/lỗi, tiện ích JSON sai, nội dung HTML từ người dùng, không có kết quả và danh sách yêu thích trống.
6. Review mobile 320/390 px bằng trình duyệt trên host với dữ liệu thử riêng; kiểm tra không tràn ngang ở kích thước lớn hơn. Outline tablet/desktop để gói riêng.

Hướng lịch xem phòng đã chốt trong `tenant-appointment-decisions.md` tiếp tục được giữ; gói 3A không thay quyết định 6 giờ hoặc nguồn khung giờ chủ nhà mở.

## Bàn giao triển khai ngày 15/10/2026

Đã thêm controller `/Tenant/RentalPost` và helper truy vấn `TenantRentalDiscovery`, ViewModel bộ lọc/thẻ/chi tiết, giao diện Razor danh sách/chi tiết/tin không còn khả dụng, gallery, CSS/JavaScript và hình thay thế cho ảnh. Danh sách yêu thích đã chuyển sang thẻ phòng, cho bỏ lưu tin cũ nhưng không hiển thị dữ liệu riêng của tin đã nháp/ẩn/đã thuê. Các đường Details/Edit/Delete cũ của Favorite cũng được giới hạn để không lộ lại thông tin đó.

`Favorite/Set` nhận trạng thái mong muốn, có CSRF, kiểm tra user và tin hợp lệ; gửi lặp không đảo trạng thái. Có JSON cho Fetch và redirect local kèm thông báo khi dùng form thông thường. Lối vào tìm phòng từ Dashboard và menu dành cho Tenant chuyển sang route mới.

Build thành công. **73/73 kiểm thử đạt** sau cải tiến, gồm các kiểm tra bảo vệ dữ liệu trước đó và kiểm tra thêm bộ lọc kết hợp, năm cách sắp xếp, phân trang, page rất lớn, dữ liệu số/enum sai, NaN/Infinity, ảnh/link không an toàn, tiện ích JSON lỗi, mã HTML và tin yêu thích đã mất khả dụng. Hai kiểm tra bổ sung xác minh bỏ một bộ lọc giữ nguyên các điều kiện khác và phân trang hợp lệ khi bỏ lưu mục cuối trang. Các warning nullable còn lại nằm ở EmailSender và các scaffold Admin/Landlord/tin đăng công khai; phần Tenant mới không thêm warning trong build cuối.

Đã kiểm tra trình duyệt Chromium trên host MVC riêng: thao tác lưu/bỏ lưu từ danh sách/chi tiết, cập nhật danh mục quận/huyện khi đổi thành phố, lỗi khoảng giá, phân trang Fetch và nút Back, gallery, lỗi lưu/thử lại, yêu thích trống, không có kết quả, lỗi mạng giữ kết quả cũ, phản hồi lọc trả về chậm không ghi đè tiêu chí mới và ảnh lỗi dùng hình thay thế. Form tìm kiếm và yêu thích vẫn hoạt động khi tắt JavaScript. Các màn danh sách/chi tiết/yêu thích/rỗng/tin mất khả dụng đã được kiểm tra ở 320/390/768/1280 px, không phát hiện tràn ngang. Đã xem ảnh màn danh sách và chi tiết mobile.

Chưa chạy ứng dụng chính với SQL Server của nhóm và chưa nghiệm thu độ khớp từng pixel với Figma. Kiểm thử dữ liệu dùng SQLite trong bộ nhớ; truy vấn giá trên SQL Server vẫn sắp xếp bằng decimal, riêng SQLite dùng cast số thực do giới hạn sắp xếp decimal của provider. Không thay model, enum, DbContext, migration hoặc database nhóm. Nội dung `Program.cs` trước gói này được giữ nguyên, đã đối chiếu SHA256. Chưa commit/push.

### Bản chạy thử để review

Mở [gói 3A đang chạy trên máy](http://127.0.0.1:8770/Tenant/RentalPost).

1. Mở bộ lọc, thử khu vực/giá/diện tích/loại phòng và sắp xếp; chuyển trang rồi dùng nút Back của trình duyệt.
2. Bấm tim trên thẻ, vào chi tiết rồi lưu/bỏ lưu; mở **Đã lưu** ở góc trên để đối chiếu kết quả.
3. Vào phòng đầu tiên để thử đổi ảnh gallery. Ảnh trong dữ liệu mẫu là hình minh họa, không phải ảnh phòng thật.
4. Thử từ khóa không tồn tại hoặc giá tối thiểu lớn hơn giá tối đa để xem trạng thái rỗng/lỗi.
5. Xem trên cửa sổ hẹp. Nút đặt lịch đang ghi **Sắp có**; gói này không nối sang form giờ tự do. Bộ lọc tiện ích vẫn chờ quy ước dữ liệu với Landlord.

Host chạy thử nằm trong project kiểm thử, dùng MVC/controller/Razor thật và database riêng trong bộ nhớ. Banner đầu trang ghi rõ dữ liệu minh họa. Chế độ này tự cung cấp một Tenant mẫu và chỉ nghe tại `127.0.0.1:8770`; không đăng ký cơ chế đó trong ứng dụng chính, không chạy `Program.cs` của ứng dụng hoặc kết nối SQL Server nhóm. Các đường dẫn ngoài gói tìm phòng/yêu thích bị giới hạn trong host thử. Dữ liệu mẫu khôi phục khi khởi động lại host; các thao tác lưu không được chuyển sang database nhóm.

Chạy lại từ thư mục dự án khi cần:

```powershell
dotnet test tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj
dotnet run --project tests/TenantSecurity.Tests/TenantSecurity.Tests.csproj -- --preview
```

Các điểm phân quyền ngoài Tenant trong `tenant-security-review.md` vẫn cần nhóm xử lý riêng. Gói 3A chỉ hoàn thiện phạm vi khám phá đã duyệt, không khắc phục những đường truy cập ngoài phạm vi đó.

## Cải tiến sau khi Dương review gói 3A

Dương đồng ý giao diện và cho phép chủ động cải tiến trong phạm vi 3A. Đã thực hiện:

- Hiển thị nhãn từng điều kiện đang áp dụng và số điều kiện trên bộ lọc; bỏ từng nhãn hoặc xóa tất cả. Bỏ tỉnh/thành phố đồng thời bỏ quận/huyện phụ thuộc. Bỏ một nhóm giá/diện tích xóa cả hai đầu khoảng, giữ các điều kiện khác và quay về trang 1. Các link này vẫn hoạt động khi JavaScript tắt.
- Kiểm tra khoảng giá/diện tích ngay khi nhập, báo lỗi tiếng Việt, đánh dấu trường sai và không gửi truy vấn khi khoảng chưa hợp lệ. Giữ kết quả cũ để người dùng sửa. Server vẫn kiểm tra lại; thông báo lỗi số không hợp lệ được chuyển sang tiếng Việt.
- Giữ trạng thái yêu thích mà server vừa xác nhận khi một kết quả lọc cũ trả về muộn. Khóa thao tác lưu theo ID phòng trong lúc đang gửi, kể cả khi danh sách được thay mới; không đổi biểu tượng theo dữ liệu cũ và không gửi lặp khi bấm vào nút vừa được dựng lại.
- Khi bỏ lưu mục cuối ở trang yêu thích cuối cùng, đồng bộ cả danh sách và địa chỉ về trang còn hợp lệ. Bổ sung khoảng cuộn an toàn cho điều khiển gần thanh điều hướng dưới, hỗ trợ thao tác khi dùng bàn phím hoặc tắt JavaScript.

Đã kiểm tra bằng trình duyệt với phản hồi cố tình giữ chậm: kết quả lọc chụp trước thao tác lưu không ghi đè biểu tượng sau khi lưu thành công; yêu cầu lưu chưa trả lời vẫn khóa nút sau khi thay danh sách. Đã kiểm tra lỗi khoảng nhập không phát sinh request, bỏ nhãn giữ bộ lọc khác, nhãn/counter sau Fetch, bỏ mục cuối trang cập nhật URL, link khi tắt JavaScript và hiển thị ở 320/390/768/1280 px. Đã xem ảnh mobile có các nhãn bộ lọc. Không đổi schema hoặc database nhóm; host minh họa được khởi động lại và dữ liệu yêu thích mẫu được khôi phục sau kiểm thử.
