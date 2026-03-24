from __future__ import annotations

import ctypes
import json
from ctypes import wintypes
from pathlib import Path

from PIL import Image


WINDOW_TITLE = "7 Days To Die"
OUTPUT_DIR = Path(__file__).resolve().parents[1] / "logs"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wintypes.DWORD),
        ("biWidth", wintypes.LONG),
        ("biHeight", wintypes.LONG),
        ("biPlanes", wintypes.WORD),
        ("biBitCount", wintypes.WORD),
        ("biCompression", wintypes.DWORD),
        ("biSizeImage", wintypes.DWORD),
        ("biXPelsPerMeter", wintypes.LONG),
        ("biYPelsPerMeter", wintypes.LONG),
        ("biClrUsed", wintypes.DWORD),
        ("biClrImportant", wintypes.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [
        ("bmiHeader", BITMAPINFOHEADER),
        ("bmiColors", wintypes.DWORD * 3),
    ]


def find_window():
    user32 = ctypes.windll.user32
    hwnd = user32.FindWindowW(None, WINDOW_TITLE)
    if not hwnd:
        return None

    rect = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    return {
        "hwnd": hwnd,
        "left": rect.left,
        "top": rect.top,
        "width": rect.right - rect.left,
        "height": rect.bottom - rect.top,
    }


def capture_window(hwnd: int, width: int, height: int) -> Image.Image:
    user32 = ctypes.windll.user32
    gdi32 = ctypes.windll.gdi32

    hdc_window = user32.GetWindowDC(hwnd)
    hdc_mem = gdi32.CreateCompatibleDC(hdc_window)
    hbitmap = gdi32.CreateCompatibleBitmap(hdc_window, width, height)
    gdi32.SelectObject(hdc_mem, hbitmap)

    # PrintWindow avoids interacting with the game process. On D3D windows this can still
    # return a mostly-black image, which we report explicitly instead of pretending success.
    PW_RENDERFULLCONTENT = 2
    user32.PrintWindow(hwnd, hdc_mem, PW_RENDERFULLCONTENT)

    bitmap_info = BITMAPINFO()
    bitmap_info.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bitmap_info.bmiHeader.biWidth = width
    bitmap_info.bmiHeader.biHeight = -height
    bitmap_info.bmiHeader.biPlanes = 1
    bitmap_info.bmiHeader.biBitCount = 32
    bitmap_info.bmiHeader.biCompression = 0

    buffer = ctypes.create_string_buffer(width * height * 4)
    gdi32.GetDIBits(hdc_mem, hbitmap, 0, height, buffer, ctypes.byref(bitmap_info), 0)
    image = Image.frombuffer("RGBA", (width, height), buffer, "raw", "BGRA", 0, 1).convert("RGB")

    gdi32.DeleteObject(hbitmap)
    gdi32.DeleteDC(hdc_mem)
    user32.ReleaseDC(hwnd, hdc_window)
    return image


def crop(image: Image.Image, left_ratio: float, top_ratio: float, right_ratio: float, bottom_ratio: float) -> Image.Image:
    width, height = image.size
    return image.crop(
        (
            int(width * left_ratio),
            int(height * top_ratio),
            int(width * right_ratio),
            int(height * bottom_ratio),
        )
    )


def non_black_ratio(image: Image.Image) -> float:
    data = image.tobytes()
    if not data:
        return 0.0
    count = 0
    total = len(data) // 3
    for index in range(0, len(data), 3):
        if max(data[index], data[index + 1], data[index + 2]) > 10:
            count += 1
    return count / float(total)


def bright_ratio(image: Image.Image) -> float:
    data = image.tobytes()
    if not data:
        return 0.0
    count = 0
    total = len(data) // 3
    for index in range(0, len(data), 3):
        if max(data[index], data[index + 1], data[index + 2]) > 180:
            count += 1
    return count / float(total)


def saturated_ratio(image: Image.Image) -> float:
    data = image.tobytes()
    if not data:
        return 0.0
    count = 0
    total = len(data) // 3
    for index in range(0, len(data), 3):
        r = data[index]
        g = data[index + 1]
        b = data[index + 2]
        if max(r, g, b) - min(r, g, b) > 60 and max(r, g, b) > 100:
            count += 1
    return count / float(total)


def run_check() -> dict:
    window = find_window()
    if window is None:
        return {
            "ok": False,
            "status": "window_not_found",
            "window_title": WINDOW_TITLE,
        }

    image = capture_window(window["hwnd"], window["width"], window["height"])
    output_path = OUTPUT_DIR / "cv_startup_check.png"
    image.save(output_path)

    full_non_black = non_black_ratio(image)
    top_center = crop(image, 0.35, 0.00, 0.65, 0.12)
    bottom_center = crop(image, 0.20, 0.75, 0.80, 0.98)
    bottom_left = crop(image, 0.00, 0.70, 0.28, 1.00)
    center = crop(image, 0.42, 0.40, 0.58, 0.60)

    metrics = {
        "full_non_black_ratio": round(full_non_black, 4),
        "top_center_bright_ratio": round(bright_ratio(top_center), 4),
        "bottom_center_non_black_ratio": round(non_black_ratio(bottom_center), 4),
        "bottom_left_saturated_ratio": round(saturated_ratio(bottom_left), 4),
        "center_non_black_ratio": round(non_black_ratio(center), 4),
    }

    if full_non_black < 0.10 or metrics["bottom_center_non_black_ratio"] == 0.0:
        status = "capture_unavailable"
        ok = False
        note = "PrintWindow produced a mostly black client area; the current execution context cannot reliably read the 3D frame."
    else:
        hud_like = metrics["bottom_center_non_black_ratio"] > 0.03 and metrics["top_center_bright_ratio"] > 0.01
        status = "startup_confirmed" if hud_like else "startup_not_confirmed"
        ok = hud_like
        note = "HUD-like features were found in the captured game window." if hud_like else "The capture succeeded, but HUD-like features were not strong enough to confirm startup."

    return {
        "ok": ok,
        "status": status,
        "window_title": WINDOW_TITLE,
        "window_rect": {
            "left": window["left"],
            "top": window["top"],
            "width": window["width"],
            "height": window["height"],
        },
        "screenshot_path": str(output_path),
        "metrics": metrics,
        "note": note,
    }


if __name__ == "__main__":
    print(json.dumps(run_check(), ensure_ascii=False, indent=2))
