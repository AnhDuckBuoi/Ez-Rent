# Phối hợp skill thiết kế cho EZ Rent Tenant

Ngày cập nhật: 16/10/2026. Dương cho phép bổ sung skill để cải thiện chất lượng thiết kế và phối hợp chúng thành một giao diện đẹp, thống nhất. Việc cài skill không đồng nghĩa duyệt một bản thiết kế mới hay mở rộng phạm vi chức năng.

## Bộ skill và vai trò

| Skill | Vai trò trong EZ Rent |
|---|---|
| Frontend Design | Chủ trì hướng thẩm mỹ của từng bản thiết kế: bố cục, nhịp điệu, phân cấp chữ, hình ảnh và điểm nhấn có chủ đích. |
| UI/UX Pro Max | Tra cứu có chọn lọc các phương án màu, chữ, bố cục và cách tương tác phù hợp màn cụ thể. Kết quả là tham khảo, không tự ghi đè hệ thiết kế đã duyệt. |
| Impeccable | Tinh chỉnh khoảng cách, mật độ nội dung, độ rõ của hành động, câu chữ và trạng thái; giữ nhận diện khi chỉ yêu cầu polish. |
| Web Design Guidelines | Kiểm tra chất lượng tương tác/hiển thị: bàn phím, focus, biểu mẫu, thông báo, ảnh, trạng thái tải và khả năng tiếp cận. |

Hai skill đầu đã có trước là Frontend Design và Impeccable. Hai skill mới được cài vào bộ skill cá nhân, không đưa mã của chúng vào repository EZ Rent:

- UI/UX Pro Max: https://github.com/nextlevelbuilder/ui-ux-pro-max-skill , thư mục `.claude/skills/ui-ux-pro-max`, revision `56ea9cf174ed86786187daa9de0e6247cb0747e4`.
- Web Design Guidelines: https://github.com/vercel-labs/agent-skills/tree/main/skills/web-design-guidelines , revision `063bee94c3f4df8453406c830b0a7df0f2860278`.

Đã xác minh SKILL.md và dữ liệu đi kèm hiện diện; công cụ tra cứu UI/UX Pro Max chạy thành công trên Python có sẵn với truy vấn `keyboard focus modal`, trả hai kết quả thuộc UX liên quan focus. Đây là kiểm tra cài đặt, không phải kết quả audit giao diện EZ Rent.

## Cách phối hợp ở mỗi gói thiết kế

1. Nêu kế hoạch và phạm vi để Dương review trước khi sửa giao diện. Đọc PRODUCT.md, DESIGN.md và bản thiết kế đang chạy, phân biệt phần đã duyệt với prototype.
2. Dùng Frontend Design để đề xuất một hướng chủ đạo. Với màn vận hành như lịch hẹn/tin nhắn, ưu tiên thông tin và thao tác rõ; không dùng khuôn landing page quảng cáo chỉ vì công cụ gợi ý.
3. Dùng UI/UX Pro Max cho đúng câu hỏi cần giải quyết, với truy vấn hẹp theo domain. Không mặc định đổi Razor/CSS sang React hoặc Tailwind. Nếu cần thử phương án hệ thiết kế, chỉ tham khảo đầu ra; không tự persist/force thay các quyết định đã duyệt.
4. Xây dựng bản thử hoặc giao diện trong phạm vi được duyệt, rồi dùng phần liên quan của Impeccable để tinh chỉnh. Một hướng thị giác xuyên suốt; không chắp các mẫu phong cách không liên quan vào từng màn.
5. Khi thực hiện review, đọc bản Web Interface Guidelines mới theo đường dẫn trong skill, đối chiếu các file liên quan và kiểm tra trong trình duyệt. Kiểm tra có giới hạn theo phạm vi, sửa lỗi có bằng chứng, tránh lặp tinh chỉnh không có tiêu chí.

Không cần tải toàn bộ tài liệu của cả bốn skill cho mọi chỉnh sửa nhỏ; chọn phần phục vụ công việc thực tế. Sở thích của Dương và nhận diện đã chốt có ưu tiên cao hơn kết quả tra cứu chung.

## Hướng thị giác đã biết của EZ Rent

- Giao diện sáng, hiện đại, gần gũi với người thuê; bớt rập khuôn và đơn điệu. Desktop là ưu tiên hiện tại.
- Giữ sự nhất quán của thanh điều hướng và các thành phần dùng chung. Trang chủ thiên về thông tin/lịch/thông báo, phòng là phần nhỏ; danh sách đầy đủ ở Tìm phòng.
- Ảnh thực tế khi có nguồn phù hợp, ghi đúng vai trò minh họa. Không dùng ảnh stock như bằng chứng cho tin phòng hoặc dữ liệu thật. Hỗ trợ tiếng Việt khi chọn font mới; chốt mẫu trước khi thay font toàn ứng dụng.
- Dùng khoảng cách, kích thước chữ, tương phản và bố cục để phân cấp; hiệu ứng chỉ bổ sung khi giúp người dùng hiểu hành động/trạng thái. Không tự thêm thư viện chuyển động hay framework từ gợi ý skill.
- Các bản thiết kế mới phải trình riêng để review. Việc cài bộ skill này chưa thay thế Home MVC hiện tại hoặc biến prototype v4 thành giao diện được duyệt.

## Ranh giới còn giữ nguyên

Nhánh `feature/tenant-p3`; không tự commit/push. Dương tự phối hợp dữ liệu chung và Landlord. Gói đặt lịch hiện tại chỉ giao diện/xem trước; dùng skill thiết kế không cấp phép thay model, migration hoặc mở gửi lịch thật. Dương đã mở rộng prototype Tin nhắn sang desktop/tablet/mobile; phạm vi này chưa áp dụng cho thiết kế lại toàn bộ MVC hoặc triển khai chat/hỗ trợ thật.

## Bản thử đầu tiên của bộ skill

Dương đã yêu cầu thiết kế một trang riêng để thử bộ skill. Đã dựng Tin nhắn Tenant tại [tenant-messages-design-trial.html](tenant-messages-design-trial.html); xem [hướng thiết kế và kết quả review](tenant-messages-design-trial.md). Đây là prototype riêng, chưa đổi nhận diện hoặc ghép chức năng chat vào MVC.

Dương tiếp tục yêu cầu bổ sung tablet/mobile và cho phép chỉnh những phần chưa ổn. Bản thử hiện có ba bố cục theo ngữ cảnh, trang so sánh [responsive review](tenant-messages-responsive-review.html), cùng kết quả kiểm tra trong tài liệu bản thử. Chưa ghép MVC.

## Cập nhật ghép MVC — 16/10/2026

Dương đã duyệt ghép Tin nhắn responsive và nhấn mạnh tái sử dụng mã gốc. Đã ghép phần đọc hội thoại/Message, shell/icon/pager/feedback và helper phòng có sẵn. Phạm vi gửi tin, gửi hỗ trợ và cập nhật đã đọc chưa triển khai. Tài liệu hiện tại: [Tin nhắn MVC](tenant-messages-mvc.md); các đoạn prototype phía trên là lịch sử review.
