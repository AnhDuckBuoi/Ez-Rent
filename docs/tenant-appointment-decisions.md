# Quyết định về đặt lịch xem phòng

> Cập nhật 16/10/2026: đã dựng bản thử desktop theo luồng Chi tiết phòng → Đặt lịch riêng cho phòng → Chờ xác nhận → Lịch hẹn. Xem [hướng dẫn review desktop](tenant-booking-desktop-review.md). Chưa ghép chức năng đặt lịch thật vào MVC.


Ngày chốt: 15/10/2026. Người duyệt: Dương. Phạm vi: luồng Tenant P3 và phần dữ liệu cần phối hợp với Landlord/Đức.

## Phương án đã chốt

Tenant chọn ngày, sau đó chọn một khung giờ trong các khung chủ nhà đã mở, tương tự chọn suất chiếu. Không nhập giờ tùy ý trong luồng đặt lịch thông thường.

1. **Chủ nhà quyết định khung giờ nhận khách.** Không tự sinh lịch trống cho mọi chủ nhà chỉ vì hệ thống có mẫu khung giờ. Hướng thiết kế gồm lịch lặp theo tuần và đóng/mở riêng theo ngày; cấu trúc dữ liệu và màn Landlord cần phối hợp trước khi triển khai.
2. **Tenant gửi yêu cầu trước giờ bắt đầu ít nhất 6 giờ**, tính tại thời điểm server nhận yêu cầu. Giờ hiển thị và nhập theo Việt Nam. Quy tắc này áp dụng cả khi Tenant tự sửa giờ của yêu cầu đang chờ.
3. **Giai đoạn đầu vẫn chờ chủ nhà xác nhận.** Chọn khung giờ và gửi thành công tạo yêu cầu `Pending`, chưa phải lịch `Confirmed`. UI phải ghi rõ: “Đang chờ chủ nhà xác nhận — vui lòng chưa đến xem phòng.”
4. **Kiểm tra trùng mốc hẹn theo người phụ trách dẫn xem**, tính cả các phòng khác do cùng người đó phụ trách. Theo cập nhật mới, chỉ chốt giờ đến; thời lượng xem do hai bên tự sắp xếp, không áp 30 phút hoặc khoảng nghỉ 15 phút. Vì không có giờ kết thúc, bản đầu chưa bảo đảm các buổi xem không kéo dài chồng lên nhau; chủ nhà chịu trách nhiệm mở các mốc đủ cách nhau. Không được mô tả kiểm tra trùng mốc bắt đầu như bảo đảm không giao nhau về thời lượng.
5. **Ẩn hoàn toàn khung chưa đủ 6 giờ**, theo cập nhật UX của Dương sau review preview. Các khung đủ thời gian còn lại phân biệt chọn được/đã có lịch. Nếu không còn giờ phù hợp, gợi ý chọn ngày khác; phân biệt với ngày chủ nhà chưa mở lịch. Thông tin khả dụng phải được kiểm tra lại tại server khi gửi và xác nhận; màn hình có thể đã cũ hoặc nhiều người cùng thao tác.
6. **Đổi giờ qua thương lượng là luồng bổ sung.** Giữ lịch sử; không cho một bên tự thay giờ của lịch đã xác nhận. Chưa coi thảo luận trước đó là phê duyệt triển khai toàn bộ thương lượng/lịch gấp.
7. **Giữ riêng mốc hẹn cho một yêu cầu `Pending`, tối đa 2 giờ từ lúc gửi.** Hạn do server tính và lưu. Chủ nhà xác nhận trước hạn thì lịch tiếp tục được giữ; từ chối, Tenant hủy hoặc hết hạn chưa xác nhận thì nhả khung. Nhả khung không bỏ qua giới hạn 6 giờ: nếu đã quá sát giờ xem thì khung vẫn ẩn với yêu cầu mới. Cần phân biệt hết hạn phản hồi với chủ nhà chủ động từ chối; biểu diễn dữ liệu sẽ do phần tích hợp chốt, chưa thêm enum trong gói này.
8. **Dương tự phối hợp/ghép Tenant và Landlord.** Phạm vi trợ lý là Tenant cùng tài liệu bàn giao. Không tự nhận thêm phần mở lịch Landlord hoặc sửa model/migration dùng chung từ câu trả lời này.

Ví dụ minh họa: tại 07:00, khung 09:00 cùng ngày bị loại bởi giới hạn 6 giờ; khung 14:00 có thể được chọn nếu chủ nhà đã mở và chưa bị lịch khác chiếm. Khung 15:00 đã có lịch xác nhận thì không chọn được. Các giờ này chỉ minh họa, không phải lịch hoạt động cố định toàn hệ thống.

## Còn cần chốt trước khi triển khai nghiệp vụ tương ứng

| Điểm cần chốt | Trạng thái |
|---|---|
| Độ dài khung | Đã chốt chỉ hẹn giờ đến, không quy định thời lượng xem cố định. |
| Khoảng nghỉ/di chuyển | Không áp mức cố định; chủ nhà mở các mốc phù hợp lịch thực tế. |
| Người dẫn xem | Cần thống nhất giai đoạn đầu có dùng chính tài khoản chủ nhà làm người dẫn hay hỗ trợ người được phân công. |
| Giữ khung khi `Pending` | Đã chốt giữ riêng cho một yêu cầu, theo người dẫn và mốc giờ trên các phòng. |
| Hạn chủ nhà phản hồi | Đã chốt 2 giờ kể từ lúc gửi, khác với mốc 2 giờ trước buổi xem từng được thảo luận. Hết hạn chưa xác nhận thì tự nhả; backend chưa triển khai. |
| Hủy lịch trước giờ xem | Hướng đề xuất là chỉ hủy trước giờ xem, giữ lịch sử; thời hạn cụ thể và xử lý hủy sát giờ cần review. |
| Lịch gấp, đề xuất giờ khác, tự xác nhận | Để luồng bổ sung sau. Giai đoạn đầu không miễn giới hạn 6 giờ và không tự xác nhận. |

## Chênh lệch với source hiện tại

`ViewingAppointment` hiện lưu `RentalPostId`, `TenantId`, `ViewingDate`, ghi chú hai bên, trạng thái và timestamps. Các trạng thái hiện có là `Pending`, `Confirmed`, `Completed`, `Cancelled`, `Rejected`.

Model này chưa lưu mốc chủ nhà công bố, người dẫn tại lúc đặt, thời hạn giữ khung hoặc lịch sử đề xuất đổi giờ. Vì vậy không thể hoàn thiện nguồn khung giờ thật chỉ bằng việc đổi input sang các nút chọn giờ. Không tự bổ sung trạng thái hết hạn hoặc sửa schema dùng chung khi chưa có phương án tích hợp được Dương/Đức thống nhất.

Gói bảo vệ dữ liệu trước đó đã có kiểm tra Tenant/owner và trạng thái. Nó **chưa triển khai** nguồn khung giờ hoặc giới hạn báo trước 6 giờ. Quyết định trong tài liệu này chưa làm thay đổi hành vi đang chạy.

## Kế hoạch gói 3 để review trước khi sửa chức năng

1. Dựng trang tìm phòng và chi tiết theo P3 với tin công khai hợp lệ, bộ lọc, phân trang và thao tác yêu thích.
2. Dựng preview mobile cho chọn ngày/khung giờ, gửi yêu cầu, danh sách và chi tiết lịch hẹn. Preview dùng dữ liệu minh họa có nhãn rõ để review bố cục và các trạng thái chọn được/quá sát giờ/đã có lịch/hết khung/chờ xác nhận.
3. Theo cập nhật đã chốt, chỉ giờ đến và giữ riêng Pending trong 2 giờ. Chuẩn bị hợp đồng nguồn mốc/người dẫn/hạn phản hồi để Dương tự phối hợp ghép với Landlord/Đức. Không hiển thị khung mẫu như khung thật của chủ nhà trong ứng dụng.
4. Sau khi có nguồn khung giờ được thống nhất, ghép luồng đặt lịch thật, kiểm tra giới hạn 6 giờ phía server và xác nhận không trùng. Kiểm thử quyền sở hữu, ranh giới thời gian và hai yêu cầu cạnh tranh cùng khung bằng dữ liệu riêng.
5. Review mobile trước; outline tablet/desktop trình riêng.

Dương đã chốt hướng nghiệp vụ và phạm vi phối hợp như trên. Đã cập nhật preview mobile bản 02; các bước ghép đặt lịch thật, thay đổi model/DbContext/migration và phần Landlord chưa triển khai, cần phối hợp theo hợp đồng dữ liệu.

## Preview mobile bản 02 để Dương review

File: [tenant-appointment-preview.html](tenant-appointment-preview.html), bản xem thử độc lập tại `http://127.0.0.1:8766/tenant-appointment-preview.html?v=2`. Dữ liệu mẫu trong bộ nhớ trang, không gửi đến chủ nhà thật và không kết nối database. Bản 02 thay bản 01 có khoảng xem 30 phút minh họa.

1. Giữ đồng hồ 07:00 ngày 15/10/2026. Chỉ hiện giờ đến; giờ dưới 13:00 ẩn, đúng 13:00 đủ 6 giờ nên chọn được. 15:00 là lịch bận của cùng người dẫn ở phòng khác.
2. Chọn giờ → Tiếp tục → nhập ghi chú → Gửi yêu cầu. Yêu cầu Pending hiển thị Giữ khung đến 09:00, nhắc chưa đến xem khi chưa xác nhận. Thời lượng xem do hai bên sắp xếp.
3. Mở ngày 16/10. 14:00 đang có người khác giữ; 16:00 là yêu cầu mẫu của bạn, cùng có hạn 09:00 hôm nay. Đổi đồng hồ 08:59 thì cả hai vẫn khóa; 09:00 thì nhả. Vì còn đủ 6 giờ, các mốc ngày mai được chọn lại. Không hiện danh tính hoặc deadline của người giữ khác trong điện thoại.
4. Vào chi tiết yêu cầu mẫu để xem Hết hạn phản hồi, ghi chú được giữ và nút Chọn lịch mới. Khác với Bị từ chối. Hết hạn không tự sống lại khi kéo đồng hồ demo lùi; bấm Khôi phục dữ liệu mẫu để làm lại.
5. Khôi phục → mở chi tiết yêu cầu đang chờ → đổi 08:59 → bấm Chủ nhà xác nhận ở bảng bên ngoài điện thoại → đổi 09:00. Lịch vẫn Đã xác nhận và mốc vẫn bận. Thử Chủ nhà từ chối hoặc Người thuê hủy ở bảng demo để xem khung được nhả trước hạn. Các nút này chỉ mô phỏng, không cấp Tenant quyền xác nhận thực tế.
6. Gửi cho mốc 13:00 hôm nay lúc 07:00, sau đó đổi 09:00. Yêu cầu hết hạn nhưng 13:00 vẫn ẩn vì chỉ còn 4 giờ báo trước. Khung được nhả không miễn quy tắc 6 giờ. Gửi lúc 08:00 thì deadline là 10:00, không cố định 09:00 cho tất cả yêu cầu.
7. Các tình huống lỗi gửi một lần/khung vừa mất vẫn giữ ghi chú; thử lại hoặc chọn giờ khác. Có ngày kín, ngày chưa mở lịch, lỗi tải, danh sách trống và chuyển tuần.

Đã kiểm tra bằng Chromium: đúng 6 giờ, chưa/đúng hạn giữ 2 giờ, giữ riêng của mình/người khác, xác nhận trước hạn và không hết hạn sau xác nhận, nhả sau từ chối/hủy/hết hạn, không đặt lại mốc dưới 6 giờ, ghi chú encode/giữ khi lỗi, chặn gửi lặp và lỗi khung vừa mất. Các màn đã thử không tràn ngang ở 320/390/768/1280 px, không có lỗi JavaScript. Đã xem ảnh màn chọn giờ, Pending có deadline và Hết hạn phản hồi. Preview không thay enum, model, controller hoặc database ứng dụng.
