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
- `actionDigest/resultDigest` giữ phạm vi đối chiếu hành động/kết quả cũ. Run schema 2 có thêm `finalStateHash`/`final_state_hash`; trace schema 2 có hash trước/sau mỗi hành động. Chỉ báo cáo replay riêng mới xác nhận đã chạy kiểm chứng; việc có hash chưa tự chứng minh replay đạt.
- Không sửa luật, animation hoặc bot để làm đẹp kết quả. Nếu thay phase/API/kiểu dữ liệu khiến runner không hiểu, công cụ phải báo lỗi rõ để cập nhật adapter.
- Bộ chạy bắt đầu từ GameConfig.CreateDefault và bộ chuyển đổi dữ liệu bài thật mỗi batch; thay cách chấm điểm trong core được áp dụng sau rebuild. Animation không được nạp.

## Kiểm thử và lưu ý
- Xem [TESTING_NOTES.md](TESTING_NOTES.md) để đối chiếu các ca đã đạt, các sửa đổi rà soát và những ca tích hợp còn chờ.
- Sau khi build runner, chạy `powershell -NoProfile -File .\Tools\Simulation\Test-Runner.ps1` để kiểm tra 10 ca chấp nhận. Cấu hình/log/báo cáo riêng ở `Tools/Simulation/Control/Acceptance/`; dữ liệu trận ở `SimulationResults/`, đều trên D. Bộ thử chỉ dừng tiến trình của chính nó và dùng file riêng để thử lỗi ghi.
- Đợi Unity biên dịch xong trước khi build. Dùng **Card Game > Simulation > Export Latest Build Diagnostics** để xuất chi tiết lỗi/cảnh báo gần nhất ra `Tools/Simulation/Control/latest-build-report.json`.
- Công cụ build nay yêu cầu biên dịch và chờ assembly reload thành công trước BuildPlayer; lỗi biên dịch chặn build. Lần build đã kiểm chứng sau sửa đạt 0 lỗi/0 cảnh báo.
- Summary nhẹ cập nhật mỗi 10 trận và ở trận cuối. Khoảng tin cậy được tính khi batch hoàn tất.

## Replay và hợp đồng dữ liệu AI

- Build runner mới, chạy một lô nhỏ với `detailedMatches` đủ cho các trận muốn xuất dữ liệu.
- Tại thư mục dự án, chạy `powershell -NoProfile -File .\Tools\Simulation\Replay-Simulation.ps1 -RunDirectory .\SimulationResults\<run> -TraceName <match_id>.jsonl -ExportAi`.
- Ví dụ với lô kiểm thử đã tạo: `powershell -NoProfile -File .\Tools\Simulation\Replay-Simulation.ps1 -RunDirectory .\SimulationResults\20261110_062015_7ab0ec9e -TraceName s0_seed2001_leg0.jsonl -ExportAi`. Thay tên lô/trace khi dùng dữ liệu khác.
- Mỗi lần kiểm chứng tạo thư mục riêng `<run>/replay/<UTC>_<id>/` trên D: `report.json`, `player.log` và `transitions.jsonl` nếu bật `-ExportAi`. Không sửa trace hay lô nguồn.
- Replay dựng lại seed/Deck, thực hiện đúng các hành động đã ghi qua GameSession/GameRules; không gọi bot hay animation. Kiểm tra state hash, hành động hợp lệ, thứ tự legal candidates, observation/policy input, payload/thứ tự events, reward và kết quả cuối.
- Report `verified-match` xác nhận cả trận; `verified-prefix` chỉ xác nhận đoạn đã ghi trước giới hạn. Lỗi trả mã khác 0, có sequence và component đầu tiên lệch. Trace thiếu đoạn/end hoặc khác phiên bản bị từ chối.
- Run/trace cũ schema 1 giữ nguyên và không có đủ hash để kiểm chứng toàn bộ trạng thái. Không tự nâng cấp hay khẳng định đã xác minh các baseline cũ. Replay v1 yêu cầu cùng runtime source fingerprint, catalog, rules/RNG version và môi trường Unity/runtime; chưa hỗ trợ tương thích ngược khi đổi luật/code.
- State hash schema 1 đọc toàn bộ trường instance (kể cả private) của đồ thị trạng thái thuần qua reflection chỉ đọc: RNG, thứ tự Deck, damage/stat adjustments, cooldown/effect uses, skip schedules, modifier lifetimes, chain và pending choices. Giữ thứ tự danh sách, chuẩn hóa dictionary/set, ghi quan hệ tham chiếu; bỏ chi tiết triển khai collection như capacity. Kiểu/comparer không hỗ trợ gây lỗi rõ thay vì âm thầm bỏ trạng thái. Chỉ kiểm chứng Windows/Mono; không phải API khôi phục snapshot hay triển khai Android/IL2CPP.
- Hash nhất quán kiểm chứng tính tái lập trên cùng phiên bản; kiểm thử luật vẫn cần để xác nhận hành vi đúng luật. Thời gian hash/ghi dữ liệu nằm ngoài phép đo bot chọn nước, nhưng có trong wall time và có thể làm chạm giới hạn thời gian sớm hơn.

Hợp đồng AI hiện tại schema 3 nằm trong `run.json/aiContract` và `SimulationAiContract.Describe()`:

- Chỉ đưa `policyInputJson` cho model: 25 global features (gồm exhaustion level và pending End-step damage của hai bên), danh sách entity với cardId công khai và 18 features/entity, danh sách hành động hợp lệ với source/target/tribute là chỉ số entity cục bộ. Mỗi action có 4 features; legal mask toàn 1 vì danh sách đã lọc hợp lệ. Dữ liệu rules v4-v5 dùng schema 2 với 21 global features và phải giữ manifest riêng.
- Numeric scales là hằng số định dạng, không clamp dữ liệu. Mô tả kèm ánh xạ tên/giá trị enum. cardId là nhãn categorical: dùng từ vựng catalog công khai cố định, không suy ra từ Deck riêng của từng trận.
- Không có seed, thứ tự Deck, danh tính tay đối thủ hay raw instanceId trong policy input. Lựa chọn hợp lệ chưa có định nghĩa trong view được mã hóa opaque; bổ sung thông tin lựa chọn cần phiên bản hợp đồng rõ ràng.
- `selectedActionIndex` trỏ đúng ứng viên tại thời điểm chọn. Episode transitions nối từ một quyết định đến quyết định tiếp theo của cùng người chơi, đi qua phản ứng đối thủ và bước tự động. Dòng xuất có thể xen kẽ thứ tự sequence giữa hai người; sequence dùng đối chiếu trace.
- Reward cuối trận: thắng +1, thua -1, hòa 0, các bước chưa kết thúc 0. Transition cuối của cả hai phía nhận reward/terminated đúng. Trận giới hạn có truncated=true, không coi là thua; bước đầu nên dùng episode hoàn tất để train, vì đoạn cắt có thể dừng trước lượt quyết định tiếp của actor.
- Metadata của `dataset_start` và file run.json là dữ liệu chẩn đoán, không phải đầu vào model. Xuất dataset chỉ từ trace chi tiết đã replay đạt; ghi vào `.tmp` rồi công bố file hoàn chỉnh khi toàn trace hợp lệ. Nếu thất bại, `.tmp` chỉ để chẩn đoán.
- Nếu học bắt chước một bot cụ thể, lọc player của transition theo bot0/bot1 trong header trace nguồn; giữ nhãn phiên bản và seed đánh giá riêng. Dataset hiện lưu cả hai phía, không mặc định mọi nước đều là của Normal.
- Chưa có trainer, Gym adapter, model, UI playback hoặc bộ clone trạng thái để Hard tìm kiếm nhánh.

Kiểm tra tập trung: **Card Game > Simulation > Verify Replay State and AI Contract** kiểm tra hash/private state và quyền riêng tư. `powershell -NoProfile -File .\Tools\Simulation\Test-Replay.ps1` tạo 6 trận nhỏ, kiểm chứng/xuất dữ liệu và thử log bị sửa, sai phiên bản, thiếu end, cùng một đoạn ActionLimit; tất cả file thử nằm trên D.
