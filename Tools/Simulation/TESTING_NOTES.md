# Ghi chú rà soát và kiểm thử bộ chạy trận

Ngày rà soát 9 November 2026; bổ sung kiểm thử chấp nhận 10 November 2026. Phạm vi gồm runner, thu thập dữ liệu, báo cáo, build và launcher.

## Kết quả đã quan sát

- Bản Windows trước đợt rà soát build thành công, 0 lỗi và 0 cảnh báo.
- Lô `SimulationResults/20261109_154412_2de60eaa/` có đủ 30 trận Finished trong CSV và JSONL, 0 lỗi, 0 trận bị cắt.
- Lô `SimulationResults/20261109_154612_d2cfce05/` tái lập 6/6 chuỗi hành động và kết quả của cùng seed. Kiểm tra 789 quyết định, gồm 355 quyết định Normal, thấy nước chọn thuộc danh sách hợp lệ và thông tin tay đối thủ được che.
- Sau các sửa đổi bên dưới, toàn bộ mã runtime liên quan đã biên dịch riêng với assembly Unity thật. Bộ kiểm tra logic chạy bằng Mono đạt PASS tại `SimulationResults/verification_20261109_160054_f0f43bf4/`.
- Kiểm tra hồi quy launcher cho file tiến độ chứa JSON lỗi đạt PASS. Watchdog dừng tiến trình thử sau khoảng 2,7 giây với ngưỡng 2 giây. Nhật ký kiểm tra ở `Tools/Simulation/Control/Review/`.
- Bản EXE sau rà soát ngày 9 November có 1 cảnh báo: build khi mã Editor thay đổi nhưng chưa biên dịch xong, có thể dùng mã xử lý trước/sau build cũ. Ngày 10 November đã build lại qua điểm gọi CLI sau khi Unity nạp mã Editor: Succeeded, 0 lỗi, 0 cảnh báo. Báo cáo ở `Tools/Simulation/Control/latest-build-report.json`; báo cáo cảnh báo cũ ở `Tools/Simulation/Control/previous-build-report.json`.
- Lô `SimulationResults/20261109_160625_8f79a86a/` hoàn tất 6/6 trận qua launcher bản mới, trùng actionDigest/resultDigest với dữ liệu gốc. Bộ đếm cặp cùng bot và tên file trong bản ghi end của trace đã khớp.
- Lô `SimulationResults/20261109_160702_fbd2a424/` ghi đủ 6/6 trận ActionLimit, winner = -2 và 0 lỗi. Cấu hình giữ warmupMatches = 2, xác nhận warm-up bị cắt vẫn cho phép chạy batch.
- Bộ `Test-Runner.ps1` đạt 10/10 ca: giới hạn lượt/thời gian, tắt trace, 5 đầu vào sai, ngắt batch và lỗi ghi file chỉ đọc. Báo cáo ở `Tools/Simulation/Control/Acceptance/20261110_061854/report.json`. Hai ca giới hạn ghi đủ 2 trận/ca và giữ trace; tắt trace hoàn tất 2 trận và không giữ trace. Batch bị ngắt phục hồi được đủ 3 trận đã xác nhận; lỗi ghi trả mã 1 và giữ nguyên file đích.
- Bản build không cảnh báo giữ nguyên fingerprint mã runtime `c381754b9d7ade75aabfd188f5cce7e848c4cb12bbdcedab31ac70bfb00c9e46` của bản dùng trong 10 ca trên. Chỉ bổ sung công cụ kiểm thử và xuất báo cáo Editor; không sửa Core, luật, bot hay UI.

## Những điểm đã sửa trong đợt rà soát

- Tính khoảng tin cậy một lần khi batch hoàn tất. Summary nhẹ cập nhật mỗi 10 trận và ở trận cuối, giảm chi phí khi chạy nhiều seed.
- Đếm cặp seed hoàn tất độc lập với loại bot, áp dụng cả Easy/Easy và Normal/Normal.
- Warm-up ưu tiên matchup có hai policy khác nhau. Khi cấu hình chỉ gồm các cặp cùng policy, công cụ luân phiên các mẫu trong số lượt warm-up đã chọn.
- Warm-up được phép chạm giới hạn thử nghiệm. Lỗi thật vẫn dừng batch.
- Lỗi quyền ghi file đi lên cấp batch. Các lỗi mở/đóng file kết quả và hoàn tất batch đi qua cơ chế báo lỗi.
- Ghi tên trace vào kết quả trước khi lưu bản ghi cuối trace.
- Launcher mở file tiến độ với quyền chia sẻ cho phép thay thế file. Nhánh đọc lỗi vẫn thực hiện kiểm tra heartbeat.
- Build/retry kiểm tra lại fingerprint nguồn và catalog, kể cả việc thêm bài mới sau bước chuẩn bị.

## Chạy xác nhận bản mới

1. Trong Unity dừng Play Mode, lưu scene cần giữ, chọn Assets > Refresh và đợi biên dịch.
2. Chạy Card Game > Simulation > Build Headless Windows Runner. Công cụ gọi bộ kiểm tra trước khi build.
3. Sau build, mở PowerShell tại thư mục dự án và chạy `powershell -NoProfile -File .\Tools\Simulation\Run-Simulation.ps1`.
4. Giữ lại đường dẫn kết quả mà launcher in ra. Đối chiếu run.json, matches.csv, matches.jsonl, summary.json và traces.
5. Chỉ đánh dấu đạt khi có kết quả quan sát được. Cột tình trạng bên dưới phản ánh thời điểm ghi chú.

Để chạy lại 10 ca chấp nhận trên bản runner đã build, dùng `powershell -NoProfile -File .\Tools\Simulation\Test-Runner.ps1`. Cấu hình và báo cáo riêng nằm trong `Tools/Simulation/Control/Acceptance/<giờ_máy>/`, kết quả trận vẫn ở `SimulationResults/`. Bộ thử tạo file chỉ đọc của riêng nó, khôi phục cờ ghi sau ca thử và chỉ dừng tiến trình con do nó khởi chạy. Đợi Unity biên dịch xong trước khi build; menu **Card Game > Simulation > Export Latest Build Diagnostics** xuất chi tiết báo cáo build gần nhất.

| Ca kiểm tra | Cách thực hiện và kết quả cần thấy | Tình trạng |
|---|---|---|
| Build bản mới | Compiler/BuildReport thành công, chạy từ launcher đúng đường dẫn D | Đã đạt; build 10 November: 0 lỗi, 0 cảnh báo |
| Lô mặc định | 30 dòng kết quả ở CSV và JSONL, summary.matches = 30, mỗi match_id duy nhất | Bản cũ đã đạt, cần xác nhận bản sửa |
| Cặp cùng bot | Hai nhóm Easy/Easy và Normal/Normal đếm đủ cặp hoàn tất, gồm cả trường hợp cùng loại bot | Logic và tích hợp sau sửa đã đạt với 1 seed mỗi nhóm |
| Tái lập | Chạy cấu hình cùng Deck/seed/bot hai lần, actionDigest và resultDigest trùng nhau | Bản cũ và bản sau sửa đạt 6/6 |
| Đổi phía | Với swapBots bật, mỗi pair_id có leg 0/1, giữ Deck theo ô Player, đổi tên bot điều khiển | Đã có trong lô cũ, cần giữ khi hồi quy |
| Giới hạn hành động | Sao chép cấu hình, đặt maxActions = 1, giữ warmupMatches = 2. Batch ghi các trận ActionLimit, winner = -2 | Logic và tích hợp sau sửa đã đạt 6/6 |
| Giới hạn lượt/thời gian | Dùng cấu hình nhỏ và giới hạn thấp. Trạng thái ghi TurnLimit hoặc TimeLimit, trace được giữ | Đã đạt tích hợp, 2 trận/ca, giữ warmupMatches = 2 |
| Lưu chi tiết tắt | Đặt detailedMatches = 0. Các trận Finished bỏ spool riêng sau khi ghi kết quả | Đã đạt, 2 Finished và 0 trace |
| Trace và dataset | Chạy cấu hình seedCount = 1, detailedMatches = 6. Nước chọn có trong legalActions, tay đối thủ được che, tên trace ở bản ghi end khớp kết quả | Quan sát bản cũ và logic sau sửa đạt, tên trace bản Player mới đã khớp |
| Dữ liệu cấu hình sai | Thử bot sai tên, Deck ngoài phạm vi, seedCount = 0, projectRoot trên C và JSON sai. Nhận lỗi rõ, giữ log chẩn đoán trên D | Đã đạt cả 5 ca, mã thoát khác 0 và thông báo đúng nguyên nhân |
| File tiến độ lỗi | File JSON không đọc được vẫn dẫn tới watchdog khi hết ngưỡng | Đã đạt qua tiến trình thử |
| Quyền ghi/dung lượng | Dùng file tiến độ chỉ đọc của bộ thử. Batch báo lỗi và không ghi đè file đích | Đã đạt trên Player thật; ổ đầy và lỗi ghi giữa luồng kết quả chưa thử |
| Ngắt giữa batch | Dừng đúng tiến trình runner thử nghiệm. Đối chiếu các match_id hoàn chỉnh trong CSV/JSONL | Đã đạt, giữ đủ 3/3 trận đã xác nhận; chưa có tự động resume |
| Thay dữ liệu bài/luật | Rebuild, kiểm tra fingerprint/cấu hình mới và kết quả dùng đúng luật đã sửa | Cần kiểm tra khi thực sự đổi luật |
| Chỉnh animation | Chỉ đổi trình diễn rồi rebuild runner, so hành động/kết quả với cùng đầu vào | Kiến trúc tách UI, chưa làm phép thử thay animation |

## Đọc kết quả và phục hồi lần chạy bị ngắt

- Luật trận được thực hiện qua GameSession/GameRules. Người đang có quyền tương tác quyết định phản hồi/hiệu ứng, kể cả ngoài lượt chính.
- Điểm thắng/thua và kết thúc trận lấy từ trạng thái game. Thay chỉ số, điểm hoặc xử lý chain cần rebuild để đưa code/dữ liệu mới vào EXE.
- Phần điều phối tự động hiện xử lý Recovery và Standby. Khi bổ sung phase hoặc thay hợp đồng API, kiểm tra và cập nhật adapter tương ứng.
- Finished có winner -1 là hòa theo luật. Winner -2 biểu thị chưa có kết quả. Giới hạn và lỗi được ghi thành trạng thái riêng.
- Khi tiến trình dừng giữa hai lần ghi, CSV và JSONL có thể lệch một dòng cuối. Phục hồi bằng giao của match_id đã ghi hoàn chỉnh và đối chiếu trace, giữ nguyên bản gốc để chẩn đoán.
- Summary trong lúc chạy có thể chậm tối đa 9 trận và chưa có khoảng tin cậy. Báo cáo cuối batch mới tính khoảng bootstrap đầy đủ.
- Báo cáo đã tạo trước lần rà soát giữ nguyên dữ liệu cũ. Hai nhóm cùng bot có thể ghi số cặp hoàn tất bằng 0 do lỗi bộ đếm đã sửa. Bản ghi end của trace cũ có thể chứa trace = null.
- Đọc cấu hình/rulesVersion/catalogVersion cùng fingerprint nội dung và danh sách Deck trong run.json khi so sánh các đợt phát triển.
- Hash hiện bao phủ chuỗi hành động và kết quả cuối. Kiểm chứng đầy đủ trạng thái nội bộ thuộc bước phát triển replay.

## Hiệu năng, lưu trữ và AI

- Warm-up phải đủ để policy cần đo được gọi. Với các matchup cùng policy tách riêng, đặt warmupMatches ít nhất bằng số policy tham gia. Kiểm tra cấu hình giới hạn thấp có thể kết thúc trước khi bot quyết định.
- Ghi nhận máy, runtime, chế độ trace và tác vụ chạy nền khi so thời gian. preparationMs, decisionMs, applyMs và wall time có phạm vi đo riêng.
- Energy allocated là thành phần phân tích riêng trong Energy spent. unusedAtTurnEnd cộng lượng Energy còn tại các lần kết thúc lượt được chấp nhận.
- Lô 30 trận đã đo khoảng 10,9 MB, trong đó 4 trace chi tiết khoảng 10,8 MB. Đây là số đo một lô cụ thể, dung lượng phụ thuộc chiều dài trận.
- detailedMatches lấy các trận đầu theo thứ tự job. Với cấu hình mặc định, 4 trace đầu thuộc Easy/Easy. Chọn cấu hình nhỏ bao phủ các matchup hoặc tăng số trace khi thu dữ liệu Normal.
- Các trace lỗi/bị cắt và log Control cũng tăng dung lượng. Công cụ hiện giữ lịch sử, cần quản lý hạn mức hoặc dọn các đợt thử đã hết giá trị trước khi chạy lớn.
- Dùng observationJson và legalActions đúng thời điểm quyết định khi tạo dataset. Normal có dữ liệu bổ sung công khai được mô tả trong schema.
- Metadata chứa seed và Deck đầy đủ để chẩn đoán. Chọn đầu vào policy theo hợp đồng quan sát, tránh đưa metadata chẩn đoán vào model.
- Reward hiện ghi ở kết quả kết thúc theo luật. Dữ liệu bị cắt cần được xử lý riêng khi chuyển sang môi trường học máy.
- Khả năng tiếp tục từ checkpoint giữa batch, resume giữa trận, quản lý dataset theo dung lượng và kết nối framework ML cần được phát triển tiếp khi có yêu cầu.
