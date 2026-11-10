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

## Normal tactical update — checkpoint 10 November

- Student chấp nhận khoảng 7:3 là mốc Normal hiện tại; 8:2 là mục tiêu nghiên cứu lý tưởng, không cần tiếp tục ép tỷ lệ lúc này. Giữ Easy/Normal làm đối thủ tham chiếu.
- Đã sửa chấm điểm hiệu ứng tự thân/Equip, giá trị ATK sau hiến tế, thế thủ khi không chuẩn bị được đòn đánh, ưu tiên mục tiêu ATK/rank cao, chi phí Energy theo cơ hội và hòa điểm ổn định. Core, Easy và luật không đổi trong đợt sửa bot.
- NormalBotVerification đạt PASS ngoài Unity với assembly Unity thật: hiệu ứng tự thân, không làm đổi dữ liệu lá đang sống khi quan sát, Equip, thế thủ/chuẩn bị Energy, hiến tế, mục tiêu, thứ tự đánh, hồi HP và 6 trận fixture. Unity build runner mới: 0 lỗi/0 cảnh báo.

| Bộ dữ liệu | Seed | Normal thắng | D0/D0; D1/D1; D0/D1 |
|---|---|---|---|
| Trước cải tiến | 1001–1025 | 88/150 = 58,67% | 30/50; 27/50; 31/50 |
| Normal mới, cùng đầu vào | 1001–1025 | 108/150 = 72% | 37/50; 38/50; 33/50 |
| Normal mới, seed chưa dùng | 1026–1050 | 107/150 = 71,33% | 36/50; 39/50; 32/50 |

- Mỗi bộ gồm ba cách ghép hai preset Deck, 25 seed, đổi bot giữa hai phía. Các trận cùng seed/cặp đổi phía có liên hệ; không diễn giải như 150 seed độc lập. Kết quả chỉ áp dụng phạm vi Deck hiện tại.
- Cùng đầu vào: 26 trận đổi sang Normal thắng, 6 trận đổi sang Easy thắng. Normal mới có dùng Fire Dragon 294, Tide Sprite 255, Stone Guardian 23 lần và triệu hồi Legendary Fighter 41 lần; 184/184 lượt triệu hồi của Normal ở turn 1 chọn thế thủ. Đây là quan sát kết hợp các thay đổi, chưa tách tác động từng thay đổi.
- Cả 300 trận Normal mới đều Finished, không lỗi/chạm giới hạn; đủ CSV/JSONL và 300 trace. Không có quyết định vượt ngưỡng 1.000 ms. Play Mode Normal/Auto mới và APK cập nhật chưa nghiệm thu; APK 0.1.0 có trước thay đổi này.
- Baseline đã gom (giữ bản gốc): `SimulationResults/Experiments/20261110_NormalEasy_Baseline150/`, gồm bảng chung, 150 trace, config/control logs, manifest và SHA-256.
- Kết quả Normal mới: `SimulationResults/20261110_042425_7622edfb/` (cùng seed, 276,66 MiB) và `SimulationResults/20261110_042629_d98dd7ff/` (seed mới, 288,34 MiB). Tất cả là file local trên D, không được push vào Git.
- Runtime fingerprint cũ `c381754b9d7ade75aabfd188f5cce7e848c4cb12bbdcedab31ac70bfb00c9e46`; mới `e2a5c1332e2fb8d90cd292d03bcca2d6d407bd2d799b3c9526d21346e1137a1a`. Cùng catalog fingerprint `4aa47bf6904b0201fc74ab6bb4838e4be8a16032e3d38ad50f0a534a6f14eb6b`. Các lần đo được tạo trước commit; đọc fingerprint và cardDefinitions trong run.json cùng sourceRevision.
- Cấu hình tái lập được theo dõi trong Git: `normal-vs-easy-evaluation.json` và `normal-vs-easy-holdout.json`; chạy qua launcher với `-Config`. Hai trận warm-up/bộ không tính vào số liệu; chạy tuần tự, lưu đủ trace.
- Đã gỡ các menu tạo/cấu hình/cân bằng bài cũ và nhánh tự ghi đè trong bootstrap/diagnostic; giữ gán artwork và kiểm tra dữ liệu. Build tool nay chủ động yêu cầu biên dịch rồi chờ assembly reload trước BuildPlayer.

## Replay/state hash và AI contract MVP — 10 November

- Thêm SimulationStateHash, SimulationAiContract, SimulationReplayFormat, SimulationReplay và ReplayFoundationVerification; tích hợp vào SimulationEngine/Runner. Không sửa Core, GameRules, Easy hoặc Normal.
- Runtime/test compilation đạt; ReplayFoundationVerification PASS ngoài Unity với assembly Unity thật: seed/state ổn định, không tiêu RNG, culture independence, latent ATK adjustment, private RNG, dictionary/set order, future skip, modifier lifetime, chain priority, hidden Deck/hand privacy và selected-action index.
- Runner Windows mới build 0 lỗi/0 cảnh báo. `Test-Replay.ps1` đạt 18/18: 6 trận thật replay/export, 11 bản sao cố ý làm sai (state, action, index, event hash, event payload, observation, seed, thiếu end, result digest, runtime version, legacy schema) bị từ chối đúng component; 1 ActionLimit được xác nhận là verified-prefix/truncated, không tính thua.
- Sáu trận hoàn tất kiểm chứng 1.222 hành động và 922 quyết định. Dataset kiểm tra đủ selected indices, dimensions/masks và terminal reward của cả hai phía; chỉ xuất file hoàn chỉnh sau khi replay đạt.
- Báo cáo: `Tools/Simulation/Control/ReplayAcceptance/20261110_132014_b683aa0f/report.json`. Lô nguồn: `SimulationResults/20261110_062015_7ab0ec9e/`; đoạn giới hạn: `SimulationResults/20261110_062116_6c5a775d/`. Report/dataset của từng replay nằm trong thư mục replay của lô tương ứng; bản sửa lỗi thử chỉ nằm trong Control, không sửa trace gốc.
- Đây là kiểm chứng Windows/Mono cùng phiên bản, không phải chứng minh mọi luật đã đúng, mọi tổ hợp chain đã bao phủ hoặc môi trường ML đã hoàn thiện. Hash/encode làm tăng wall time ngoài phép đo chọn nước; không so tốc độ batch mới/cũ nếu khác chế độ ghi dữ liệu.
- Hướng dẫn/schema và các giới hạn nằm trong README của công cụ. Không cập nhật hash ngược vào dữ liệu schema-1 hay thay đổi các baseline trước đó.

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
- Dữ liệu schema 1 chỉ có hash chuỗi hành động/kết quả cuối. Run/trace schema 2 có full-state hash và lệnh replay riêng như phần MVP ở trên; không diễn giải hash cũ thành kiểm chứng trạng thái đầy đủ.

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
