# Chạy trận hàng loạt không giao diện

## Chuẩn bị và chạy
1. Trong Unity, dừng Play Mode, lưu scene bạn muốn giữ rồi chọn **Card Game > Simulation > Build Headless Windows Runner**.
2. Công cụ chỉ tạo/cập nhật scene và catalog riêng trong `Assets/Simulation/`; không sửa scene đấu, hồ sơ khách hay APK.
3. Mở PowerShell tại thư mục dự án, chạy `powershell -File .\Tools\Simulation\Run-Simulation.ps1`.
4. Không cần Play Mode. Có thể giữ Unity mở; nên ngừng các tác vụ nặng khác khi đo tốc độ bot.
5. Kết quả nằm tại `D:\Unity\Projects\2D-Strategic-Card-Game\SimulationResults\<UTC_ngày_giờ>_<mã_lần_chạy>\`. Log tiến trình ở `Tools/Simulation/Control/`, cũng trên D.

## Cấu hình batch.json
- Mặc định: 30 trận thử, gồm Easy/Easy, Normal/Normal và Normal/Easy trên hai Deck mẫu thật.
- `seedCount`, `firstSeed`: số seed và seed đầu. Không dùng seed 0 để tránh giá trị thay thế của RNG.
- `deck0`, `deck1`: chỉ số Deck, bắt đầu từ 0. Tên và danh sách bài nằm trong run.json.
- `swapBots`: thêm trận đổi hai bot cho cùng seed và Deck cố định theo ô Player; không sửa người đi trước trong luật.
- `warmupMatches`: chạy khởi động, loại khỏi số liệu. `detailedMatches`: số trận đầu lưu thông tin quan sát/quyết định đầy đủ.
- `maxTurns`, `maxActions`, `maxMatchSeconds`: giới hạn ngoài luật; chạm giới hạn được ghi riêng, không tính hòa/thua.
- `decisionWarningMs`: chỉ đánh dấu chậm sau khi hàm chọn nước trả về. PowerShell giám sát heartbeat để dừng riêng tiến trình bị treo.
- Có thể đổi lựa chọn bot, số seed và Deck trong cấu hình rồi chạy lại mà không rebuild. Khi thay code bot, luật, chỉ số, chain hoặc dữ liệu bài: **rebuild runner** trước.
- So sánh Normal/Easy 150 trận: `powershell -NoProfile -File .\Tools\Simulation\Run-Simulation.ps1 -Config .\Tools\Simulation\normal-vs-easy-evaluation.json` (seed 1001–1025). Bộ seed mới dùng `normal-vs-easy-holdout.json` (1026–1050). Mỗi bộ có 50 trận cho D0/D0, D1/D1, D0/D1, đổi bot hai phía; giữ đủ 150 trace. `normal-vs-easy-pilot.json` giữ cấu hình pilot 30 trận ban đầu.

## Các file đầu ra
- `run.json`: cấu hình, luật hiện hành, catalog/Deck, phiên bản/fingerprint code và dữ liệu, môi trường đo.
- `matches.csv`: một dòng mỗi trận, mở bằng Excel. `matches.jsonl`: cùng kết quả với số liệu/event counts đầy đủ.
- `summary.json`: tổng hợp theo matchup, tỷ lệ điểm của bot tham chiếu trên cặp seed hoàn tất và khoảng bootstrap 95% khi đủ mẫu.
- `traces/*.jsonl`: giữ trận mẫu chi tiết và mọi trận lỗi/bị cắt. Trận không mẫu vẫn có spool hành động trong khi chạy; xóa spool đó khi hoàn tất bình thường.
- `progress.json`: tiến độ hiện tại. Kết quả được ghi/flush từng trận, không ghi đè lần chạy trước.

## Diễn giải và dữ liệu AI
- `Finished` + winner -1 là hòa theo luật; -2 là chưa có kết quả. `TurnLimit/ActionLimit/TimeLimit` là bị cắt. Các trạng thái khác là lỗi và không được tính như thua chiến thuật.
- Energy allocated là phần phân tích riêng, không cộng lại vào Energy spent. Unused là tổng Energy còn tại các lần kết thúc lượt được chấp nhận.
- Thời gian chọn nước không bao gồm tạo thông tin/hành động hợp lệ; các phần chuẩn bị và áp dụng luật được đo riêng. Wall time gồm cả ghi file.
- Các giá trị slot0/slot1 trong summary nói về ô Player, không cố định tên bot khi swapBots bật.
- Cặp seed có hai trận liên quan; khoảng tin cậy lấy mẫu lại cả cặp, loại cặp thiếu kết quả. Pilot nhỏ không chứng minh Normal mạnh hơn.
- Trace chi tiết lưu thông tin quan sát đã che bài ẩn, lựa chọn hợp lệ, nước chọn và reward cuối trận. Normal có bổ sung thông tin công khai mà Easy hiện không dùng; không gọi đây là hai bộ quan sát giống hệt nhau.
- Seed, Deck và catalog đầy đủ trong metadata là dữ liệu chẩn đoán, **không** phải đầu vào policy. Không lấy toàn bộ metadata làm observation cho ML.
- `actionDigest/resultDigest` phục vụ đối chiếu hành động/kết quả; không phải full-state hash hay trình replay đã kiểm chứng. Chưa phải dataset cắm trực tiếp vào một framework ML.
- Không sửa luật, animation hoặc bot để làm đẹp kết quả. Nếu thay phase/API/kiểu dữ liệu khiến runner không hiểu, công cụ phải báo lỗi rõ để cập nhật adapter.
- Bộ chạy bắt đầu từ GameConfig.CreateDefault và bộ chuyển đổi dữ liệu bài thật mỗi batch; thay cách chấm điểm trong core được áp dụng sau rebuild. Animation không được nạp.

## Kiểm thử và lưu ý
- Xem [TESTING_NOTES.md](TESTING_NOTES.md) để đối chiếu các ca đã đạt, các sửa đổi rà soát và những ca tích hợp còn chờ.
- Sau khi build runner, chạy `powershell -NoProfile -File .\Tools\Simulation\Test-Runner.ps1` để kiểm tra 10 ca chấp nhận. Cấu hình/log/báo cáo riêng ở `Tools/Simulation/Control/Acceptance/`; dữ liệu trận ở `SimulationResults/`, đều trên D. Bộ thử chỉ dừng tiến trình của chính nó và dùng file riêng để thử lỗi ghi.
- Đợi Unity biên dịch xong trước khi build. Dùng **Card Game > Simulation > Export Latest Build Diagnostics** để xuất chi tiết lỗi/cảnh báo gần nhất ra `Tools/Simulation/Control/latest-build-report.json`.
- Công cụ build nay yêu cầu biên dịch và chờ assembly reload thành công trước BuildPlayer; lỗi biên dịch chặn build. Lần build đã kiểm chứng sau sửa đạt 0 lỗi/0 cảnh báo.
- Summary nhẹ cập nhật mỗi 10 trận và ở trận cuối. Khoảng tin cậy được tính khi batch hoàn tất.
