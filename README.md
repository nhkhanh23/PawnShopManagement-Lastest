# PawnFlow — Quản lý cửa hàng cầm đồ

ASP.NET Core MVC / .NET 10 / Entity Framework Core / SQL Server. Bản mã nguồn Windows dùng cho bài tập lớn; có quản lý khách hàng, hợp đồng, thời hạn cầm và cảnh báo hạn, phân quyền Admin/Staff, tìm kiếm/lọc/phân trang, AJAX và REST API.

## Chạy trên máy mới

Máy cần cài **.NET SDK 10** và **SQL Server Express LocalDB** một lần trước khi chạy. Cài Runtime hoặc SSMS riêng là chưa đủ. Lần đầu cần Internet để tải thư viện NuGet.

- .NET SDK 10 cho Windows: https://dotnet.microsoft.com/download/dotnet/10.0
- LocalDB và hướng dẫn cài chính thức: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb
- Có thể cài LocalDB trong Visual Studio Installer → Modify → Individual components → SQL Server Express LocalDB. Sau khi cài, mở lại cửa sổ chạy.

1. Trên GitHub chọn **Code → Download ZIP**.
2. Chọn **Extract All / Giải nén tất cả** vào một thư mục có quyền ghi, chẳng hạn Documents. Không chạy trực tiếp bên trong ZIP.
3. Mở **Chay-ung-dung.cmd** bằng File Explorer. Không dùng nút Start của Visual Studio để chạy file CMD.
4. Chờ khôi phục thư viện, biên dịch, kiểm tra database. Trình duyệt tự mở khi website đã sẵn sàng.
5. Máy mới chưa có tài khoản sẽ vào trang tạo admin. Tự đặt tên đăng nhập và mật khẩu (10–128 ký tự), sau đó đăng nhập bằng tài khoản vừa tạo. Máy đã có dữ liệu sẽ vào trang đăng nhập.
6. Giữ cửa sổ CMD mở khi sử dụng; nhấn Ctrl+C để dừng trước khi cập nhật hoặc chạy lại.

Không cần mở Visual Studio và không cần nhập SQL thủ công trên máy mới có đủ phần mềm. Tài khoản và dữ liệu trên máy tác giả không được đưa lên GitHub; máy thầy tạo database và admin riêng. Không có mật khẩu admin mặc định.

## Chương trình tự làm gì?

- Tìm đúng `PawnShopManagement.csproj`, kể cả đường dẫn có dấu cách hoặc ZIP lồng thêm một cấp.
- Kiểm tra SDK 10, khôi phục NuGet và biên dịch; dừng nếu có lỗi.
- Dùng cấu hình `ConnectionStrings:DefaultConnection` trong `PawnShopManagement/appsettings.json`. Mặc định là `(localdb)\mssqllocaldb`, database `PawnShopDB`, xác thực Windows.
- Mở database có sẵn trước. Chỉ tạo khi chưa có; schema được bổ sung trong transaction, không xóa dữ liệu. Nếu database có sẵn nhưng không mở được, báo lỗi quyền/trạng thái thay vì cố tạo lại.
- Chọn cổng 5073 nếu còn trống, hoặc một cổng trống khác. Chỉ lắng nghe localhost.
- Đợi đúng tiến trình web phản hồi, rồi mở `/Account/Start` bằng trình duyệt mặc định. Có thể tự chép URL màu xanh trong CMD nếu trình duyệt không mở.
- Ghi log tại `PawnShopManagement/App_Data/logs`. Các file này không đưa lên GitHub.

## Nếu gặp lỗi

- Không tìm thấy project: giải nén toàn bộ ZIP, giữ CMD và PS1 ở cùng cấp với thư mục PawnShopManagement.
- Thiếu SDK: cài .NET **SDK 10**, không chỉ Runtime. Kiểm tra bằng `dotnet --list-sdks`.
- Không kết nối LocalDB: xác nhận đã cài LocalDB bằng `sqllocaldb info`. Có thể kiểm tra thêm `sqllocaldb info MSSQLLocalDB`. Không xóa instance/database để thử sửa lỗi vì có thể mất dữ liệu.
- Database đã tồn tại nhưng tài khoản không mở được: chạy bằng đúng tài khoản Windows sở hữu LocalDB, kiểm tra quyền và trạng thái database; có thể cấu hình một SQL Server khác trong appsettings.json. Không chia sẻ mật khẩu SQL lên repository public.
- Restore NuGet lỗi: kiểm tra Internet và khả năng truy cập nuget.org.
- Build báo file bị khóa: dừng cửa sổ đang chạy bản ứng dụng đó rồi thử lại.
- Bản launcher mới hiển thị `[PAWNFLOW-RUN-03]`; bước database hiển thị `[DB-FIX-02]`. Nếu không thấy, kiểm tra lại thư mục/bản tải xuống.

## Kiểm thử

`Verification` chứa bộ kiểm thử. `integration-results.txt` có 67 kiểm tra đạt bằng InMemory và kiểm tra dịch truy vấn SQL; đây không phải xác nhận kết nối SQL thật. `launcher-results.txt` có 10 kiểm tra launcher trên Windows PowerShell 5.1.

Muốn kiểm tra SQL thật trên máy có LocalDB, mở `Kiem-tra-SQL.cmd`. Script dùng database tên ngẫu nhiên `PawnShopVerification_...`, không dùng database nghiệp vụ `PawnShopDB`. Database kiểm thử được giữ lại để kiểm tra, không tự xóa. SQL thực tế trên máy người nhận vẫn cần được xác nhận bằng cách chạy.

## Nộp bài

- Link source: https://github.com/nhkhanh23/PawnShopManagement
- Link quản lý dự án: dùng URL bảng GitHub Projects của nhóm và bảo đảm giảng viên có quyền xem.
- Tài liệu hiện có nằm trong `Tai-lieu`; số kiểm tra trong báo cáo/slide có thể là mốc trước, kết quả mới nhất xem hai file kết quả ở thư mục gốc.
- Link GitHub là mã nguồn để tải về, không phải website đang được hosting. Bản này dành cho chạy local trên Windows.

## Đưa lại toàn bộ mã nguồn lên GitHub

Gói ZIP không chứa bin/obj, dữ liệu cá nhân, database hay khóa đăng nhập. Giữ `.gitignore` kèm theo.

Nếu dùng trình duyệt, có hai gói `GitHub-Lan-1.zip` và `GitHub-Lan-2.zip`, mỗi gói dưới 100 file. Giải nén từng gói, vào **trang gốc repository → Add file → Upload files**, kéo **nội dung bên trong** gói (không kéo ZIP, không kéo thư mục bọc mang tên GitHub-Lan-1/2), rồi Commit changes. Làm lần lượt cả hai gói tại trang gốc. File cùng đường dẫn được cập nhật.

Hoặc dùng GitHub Desktop để clone repository, chép nội dung bản đầy đủ vào thư mục clone (giữ nguyên `.git` của clone), chọn Commit to main rồi Push origin. Cách này tránh giới hạn số file của tải bằng trình duyệt.
