# Nền ứng dụng và hướng dẫn kiểm tra — 21/10/2026

Trạng thái checkpoint 21/10/2026: Unity đã tạo scene UI, prefab kết quả và các
file meta. Người dùng đã kiểm tra và yêu cầu commit checkpoint. Báo cáo kiểm tra
Editor lúc 11:49:05 UTC ghi PASS cho save/load, migration v1, giới hạn copies,
backup, bảo toàn save hỏng/schema lạ, session 30/31/40 lá, quyết định Easy Bot và
binding/trang UI. Chưa có bằng chứng xuất/chạy APK hoặc .exe; đây vẫn là bước
kiểm chứng tiếp theo, không suy ra từ kết quả Play Mode.

## Phạm vi đã triển khai trong mã

- AppShell: giao diện lưu trong scene, sửa được ở Edit Mode. Home, Guest Entry,
  Deck Builder, Profile, Settings, cửa sổ card/filter/thông báo/xác nhận.
- Home: avatar + tên ở trên trái mở Profile; Settings ở trên phải.
- Profile: đổi tên, chọn giữa các sprite avatar hiện có, lịch sử 50 trận gần nhất,
  tổng thắng/thua và tỷ lệ thắng PvE tự chơi. Auto hoặc tắt Easy Bot được đánh dấu
  luyện tập; không tính vào tỷ lệ. Rời trận sớm là Abandoned, không tính thua.
- Home → PvE: toàn bộ deck đã chọn/lưu, 30–40 lá; đối phương dùng fixed demo deck
  30 lá với Easy Bot bật sẵn. Seed mới mỗi trận. CardData/effect/core được tái sử dụng.
- Kết quả → lưu lịch sử/log → Home. Có thể thoát sớm qua Settings trong trận →
  Duel Settings → RETURN HOME; hoặc Escape/Back. Có xác nhận, không đổi luật core.
- SampleScene mở Play trực tiếp vẫn dùng demo cũ; không ghi vào hồ sơ app.
- Âm lượng master áp dụng AudioListener; Music/SFX lưu riêng và áp dụng qua
  AppAudioChannel khi có AudioSource. Chưa thêm nhạc hay âm thanh mới.
- Chưa có tài khoản online, cloud sync, PvP, collection economy, hoặc replay player.

## Tạo UI và xem trước trong Unity

1. Unity Editor, **Edit Mode**: dừng nút Play hình tam giác giữa thanh công cụ nếu
   đang sáng. Lưu scene đang chỉnh bằng Ctrl+S. Chờ Unity nhập script xong.
2. Project → Assets/Scenes → mở AppShell. UI được tạo tự động một lần khi script
   reload. Nếu Hierarchy vẫn chỉ có AppShell và App Camera, chạy menu
   **Card Game → App Shell → Prepare Editable UI**. Không chạy trong Play Mode.
3. Hierarchy → chọn object gốc **AppShell**. Inspector có các nút Preview MainMenu,
   GuestEntry, DeckBuilder, Profile, Settings, CardDetail, Filter, Message.
   Bấm một nút để xem trang tương ứng ngay trong Game view ở Edit Mode.
4. Hierarchy → AppShell/AppCanvas/SafeAreaRoot/PortraitFrame → trang tương ứng.
   Chọn object con để sửa RectTransform, Image, TextMeshPro, ScrollRect/GridLayout.
   Ctrl+S để lưu. Khi Play, bố cục/font/màu của khung này được giữ nguyên.
5. Game view → chọn tỷ lệ 9:16 hoặc Mobile 1080×1920. Kiểm tra thêm màn hình dài
   như 1080×2400 và cửa sổ ngang. App giữ khung dọc, đặt giữa, có safe area.
6. Những lá hiển thị trong deck/catalog ở Edit Mode là bản xem trước. Play sẽ
   thay chúng bằng dữ liệu thật. Chỉnh GridLayoutGroup trên DeckCards/CatalogCards
   để thay cell/spacing; Grid Card Scale trên AppShell chỉnh cỡ visual card runtime.
   Chỉnh từng lá preview không làm thay đổi những lá sẽ sinh ra khi Play.
7. ModalLayer chứa CardPanel, FilterPanel, MessagePanel, ConfirmPanel. Chỉ bật một
   cửa sổ khi preview. Trong FilterPanel có MonsterFilters và SpellTrapFilters,
   có thể bật/tắt thủ công để chỉnh mỗi tab. Logic chạy sẽ tự chọn đúng tab.
8. Result/Exit UI: Project → Assets/Prefabs/AppDuelOverlay → mở Prefab Mode,
   sửa ResultOrExitPanel cùng Title/Description/Home/Cancel rồi lưu prefab.

Các chỉnh sửa Inspector trong **Play Mode là tạm thời**. Chỉnh giao diện ở Edit
Mode hoặc ghi lại thông số rồi áp dụng sau khi dừng Play. Công cụ chuẩn bị chỉ tạo
khi chưa có UI; không tự dựng lại hay ghi đè UI đã chỉnh. Button binding lưu tham
chiếu và khóa nội bộ, nên đổi tên/di chuyển object giữ nguyên component vẫn được;
đừng xóa nút rồi tự tạo nút mới mà chưa cập nhật tham chiếu.

## Kiểm tra luồng người dùng

1. Unity Editor > AppShell, Edit Mode → nút Play ở giữa toolbar (Ctrl+P).
   Game view hiện Guest Entry. Nhập tên → ENTER GAME → Home.
2. Nhấn avatar/tên trên trái → Profile. Đổi avatar bằng < >, đổi tên → SAVE PROFILE.
   Home phải hiện đúng tên/avatar mới. Dừng/rerun Play: dữ liệu vẫn còn.
3. Settings trên phải → chỉnh ba slider → SAVE SETTINGS. Mở lại phải giữ giá trị.
   Chưa có nhạc/SFX nên hai slider đó chưa có âm thanh minh họa. Âm lượng master
   tác động tới AudioListener; AudioSource thêm sau cần AppAudioChannel để phân nhóm.
4. Deck Building → xem Starter Balance 30 lá và Guardian Control 31 lá; sửa card,
   tìm kiếm, lọc hai tab, kéo drawer. Không được thêm bản thứ 4; không quá 40 lá;
   dưới 30 lá không SAVE được. Rời/switch deck có thay đổi chưa lưu phải hỏi bỏ sửa.
5. Chọn Guardian Control, SAVE → HOME. Home hiện đúng tên deck. Nhấn PvE.
   Sau khi chia 4 lá: tổng số lá hand + deck của bạn phải là 31 (trước draw step);
   sau draw step, tổng vẫn là 31. Deck không bị cắt về 30. Đối phương là Easy Bot.
6. Có thể bật Auto để kết thúc nhanh. Đợi hoạt ảnh cuối chạy xong: màn kết quả
   xuất hiện, HOME đưa về Home. Profile có lịch sử và nhãn Auto practice. Chỉ trận
   tự chơi hoàn tất mới cập nhật tỷ lệ thắng/thua; Auto vẫn lưu đầy đủ action log.
7. Vào trận khác → Settings trong trận → Duel Settings → RETURN HOME → CONTINUE
   DUEL để hủy thử; mở lại và HOME để rời. Lịch sử thêm Abandoned, không thêm loss.
8. Mở SampleScene trực tiếp trong Project ở Edit Mode rồi Play: demo vẫn hoạt động.
   App không mang deck/profile của lần Play trước vào demo.

Nếu nhập file lưu cũ hỏng/phiên bản mới hơn, app báo lỗi và dùng deck preview tạm;
không tự ghi đè. Vào Deck Builder và SAVE rõ ràng mới thay file, giữ .backup. Lỗi
lưu kết quả sẽ hiện trên màn kết quả; HOME thử lưu lại, không báo thành công giả.

## Dữ liệu và chuẩn bị tài khoản online

- Hồ sơ hiện tại: `Application.persistentDataPath/local_decks_v1.json`.
  Tên file giữ nguyên để nâng cấp dữ liệu cũ; nội dung mới là schemaVersion 2.
- Trên Windows với company/product hiện tại, thư mục nằm trong
  `%USERPROFILE%/AppData/LocalLow/DefaultCompany/2D-Strategic-Card-Game`.
  Android dùng persistentDataPath của ứng dụng; không ghi vào Assets/Resources.
- Ghi file tạm `.pending`, thay file hiện hành và giữ `.backup`. Dữ liệu hỏng hoặc
  schema chưa hỗ trợ không bị thay tự động. Deck v1 được nâng cấp khi lưu tiếp.
- Local player ID và deck ID là GUID. Avatar hiện là chỉ số trong danh sách sprite
  đã serialize của AppShell: chỉ thêm cuối danh sách để không đổi avatar đã lưu.
- Tên, avatar, favorite, deck, âm lượng, kết quả gần đây, tổng thắng/thua nằm trong
  hồ sơ. Mỗi log ở `matches/<localPlayerId>/<matchId>.json` chứa hai deck đầu vào,
  seed, phiên bản luật/catalog/random, thời điểm và các hành động được core chấp nhận.
  Log chưa có state hash/replay verifier; không gọi đây là tính năng replay hoàn chỉnh.
- Profile repository là điểm nối giữa UI và lưu trữ. Bản hiện tại là local-only,
  một guest đang dùng trên máy. Chưa có chuyển đổi giữa nhiều tài khoản đăng nhập.

Khi làm online, cần một bước riêng với dịch vụ xác thực/server được thống nhất:

1. Dùng user ID do dịch vụ xác thực cấp sau đăng nhập. Local player ID chỉ để nhận
   diện dữ liệu guest, không chứng minh quyền sở hữu tài khoản. Không lưu mật khẩu
   trong JSON và không coi đổi tên hiển thị là đăng nhập.
2. Tách cache theo authenticated user ID; chỉ link/import deck guest khi người dùng
   xác nhận. Không trộn deck/history của hai tài khoản khi đổi người đăng nhập.
3. Token theo cơ chế lưu an toàn do SDK/nền tảng cung cấp; không đặt API secrets
   trong Unity asset, repo hoặc save. Chốt nhà cung cấp trước khi thêm SDK.
4. Server kiểm tra quyền sở hữu mỗi record, validate deck, ghi kết quả PvP từ phiên
   đấu được xác minh. Win/loss local và log do client gửi không phải bảng xếp hạng
   đáng tin. Không đồng bộ trực tiếp toàn bộ JSON thành dữ liệu có thẩm quyền.
5. Thống nhất xử lý guest→account, offline→online, xung đột revision và schema
   migration. Timestamp/revision hiện tại là đầu vào hỗ trợ, chưa giải quyết conflict.
6. Chỉ sau đó triển khai recovery tài khoản, logout, cloud backup và nhiều thiết bị.

## Build và kiểm tra kỹ thuật

- Unity Editor, Edit Mode → **Card Game → App Shell → Verify App Foundations**.
  Kiểm tra save/load, migration v1, file backup, từ chối deck sai, giữ save hỏng/
  schema lạ, deck 30/31/40 vào GameSession và 150 bước hợp lệ mỗi deck, binding UI
  trên bản sao preview. Dữ liệu thử ở Temp, không dùng save thật. Kết quả nằm trong
  Console và `Logs/AppFoundationVerification.txt`. Tự chạy một lần khi tạo UI.
- **Apply Portrait Build Defaults**: Android portrait, Windows cửa sổ 540×960,
  cho resize. Giữ company/product để không đổi đường dẫn save. Chỉ chạy Edit Mode.
- **Build Development Windows** → `Build/AppShellWindows/CardGame.exe`.
- **Build Development APK** → `Build/AppShellAndroid/CardGame.apk`.
  Các menu build yêu cầu lưu scene, chọn cả AppShell rồi SampleScene. Module Android,
  SDK/NDK/JDK đã thấy trong bộ Unity cài sẵn; điều đó chưa chứng minh APK build/chạy
  thành công. Cần kiểm tra artifact và thiết bị thực tế.
- Compilation bằng dotnet kiểm tra lỗi C#; không thay thế Play Mode, render UI,
  thao tác cảm ứng, hoặc APK/.exe. Chỉ ghi đã kiểm chứng khi đã chạy thật.

## Giới hạn công việc 22–31/10

Ưu tiên sửa scene/prefab UI, font size/màu/spacing, vùng chạm, scroll, safe area,
ảnh và kiểm tra các bước trên. Không tự dựng lại AppShell, sửa rule/effect/core,
đổi card ID, xóa `.meta`, đổi company/product, tự thêm SDK/backend hay mở rộng
collection economy. Nếu nút mất binding/không chạy, sửa binding tương ứng; không
đưa toàn bộ UI trở lại cách tạo runtime. Tạo checkpoint sau khi người dùng chấp
nhận kết quả Play và kiểm tra đủ file scene/prefab/meta do Unity tạo.
