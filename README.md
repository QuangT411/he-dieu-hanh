# Hook Logger

Ứng dụng Windows Forms được viết bằng C# (.NET 8.0) với chức năng chính là ghi lại (record) và phát lại (replay) các sự kiện chuột và bàn phím trên toàn hệ thống. Ứng dụng có giao diện hiện đại, hỗ trợ nhiều trang (Dashboard, Event Log, Settings) và quản lý giao diện (Theme).

## Tính năng chính

### 1. Event Log (Ghi và Phát lại sự kiện)
*   *Ghi lại (Record):* Sử dụng Windows API (`SetWindowsHookEx`) để bắt các sự kiện chuột (Click, Move, Wheel) và bàn phím (KeyDown, KeyUp) trên toàn cục (Global Hook).
*   **Phát lại (Replay):** Tự động thực hiện lại các thao tác đã ghi với độ trễ tương ứng.
*   **Quản lý Log:**
    *   Hiển thị danh sách sự kiện chi tiết trên bảng (DataGridView).
    *   Lưu lịch sử ghi vào file JSON.
    *   Tải lại lịch sử từ file đã lưu.
    *   Xóa lịch sử.

### 2. Dashboard
*   Hiển thị thống kê tổng quan về phiên làm việc.
*   Tổng số sự kiện đã ghi.
*   Trạng thái ghi (Đang ghi/Dừng).
*   Thời gian ghi.
*   Phân bố loại sự kiện.

### 3. Settings & Giao diện
*   Hỗ trợ thay đổi giao diện (ThemeManager).
*   Cấu hình ứng dụng.

## Công nghệ sử dụng

*   **Framework:** .NET 8.0 (Windows Forms)
*   **Windows API (P/Invoke):**
    *   `user32.dll`: Sử dụng các hàm API cấp thấp để Hook hệ thống (`SetWindowsHookEx`, `CallNextHookEx`, `UnhookWindowsHookEx`) và mô phỏng sự kiện (`mouse_event`, `keybd_event`).
## Yêu cầu hệ thống

*   Hệ điều hành: Windows 10/11.
*   .NET SDK: Phiên bản 8.0 trở lên.

## Hướng dẫn cài đặt và chạy

1.  **Clone hoặc tải dự án về máy:**
    ```bash
    git clone https://github.com/QuangT411/he-dieu-hanh
    ```

2.  **Mở dự án:**
    *   Mở file `he dieu hanh.sln` bằng Visual Studio 2022 hoặc JetBrains Rider.
    *   Hoặc mở thư mục dự án bằng VS Code.

3.  **Build dự án:**
    *   Trong Visual Studio: Nhấn `Ctrl + Shift + B`.
    *   Trong Terminal:
        ```bash
        dotnet build
        ```

4.  **Chạy ứng dụng:**
    *   Trong Visual Studio: Nhấn `F5`.
    *   Trong Terminal:
        ```bash
        dotnet run --project "he dieu hanh.csproj"
        ```


## Lưu ý

*   **Cảnh báo Antivirus:** Do ứng dụng sử dụng **Global Hook** (thông qua `user32.dll`) để bắt sự kiện chuột và bàn phím, một số phần mềm diệt virus có thể cảnh báo nhầm là mã độc. Đây là hành vi bình thường của các ứng dụng dạng Auto Click/Macro.
*   **Quyền Administrator:** Cần chạy ứng dụng với quyền **Administrator** nếu muốn tương tác (ghi/phát lại) với các ứng dụng chạy quyền cao khác (ví dụ: Task Manager, Game...).


# Tác giả đóng góp
*   Tô Mạnh Quang: Hook và ghi lại các sự kiện của chuột
*   Nguyễn Văn Quang: Hook và ghi lại các sự kiện của bàn phím
*   Hà Hữu An: Replay lại các sự kiện theo thời gian thực
*   Phạm Đức Trung: Giao diện và các nút xử lý các hàm sự kiện

# Tài liệu tham khảo
[Microsoft Windown Hook](https://learn.microsoft.com/en-us/windows/win32/winmsg/hooks)