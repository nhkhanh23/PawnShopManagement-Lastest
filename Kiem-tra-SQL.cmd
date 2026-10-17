@echo off
setlocal
cd /d "%~dp0"
set "PAWN_PROJECT_ROOT=%CD%\PawnShopManagement"
set "PAWN_CHECK_OUTPUT=%CD%\Ket-qua-SQL"
set "PAWN_SQL_TEST_CONNECTION=Server=(localdb)\mssqllocaldb;Integrated Security=True;MultipleActiveResultSets=true"
echo Kiem thu bang SQL Server tren database PawnShopVerification rieng.
echo Khong su dung hoac xoa du lieu cua PawnShopDB.
dotnet run --project Verification\Checks.csproj
echo.
if errorlevel 1 (echo KIEM THU CHUA THANH CONG. Xem loi phia tren.) else (echo KIEM THU THANH CONG. Ket qua o thu muc Ket-qua-SQL.)
pause
