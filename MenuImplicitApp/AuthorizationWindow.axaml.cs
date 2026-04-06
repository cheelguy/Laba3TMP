using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AuthorizationLibrary;
using DataMenuLibrary;

namespace MenuImplicitApp;

public partial class AuthorizationWindow : Window
{
    private readonly DispatcherTimer _keyboardStatusTimer;

    public AuthorizationWindow()
    {
        InitializeComponent();
        _keyboardStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _keyboardStatusTimer.Tick += (_, _) => UpdateKeyboardStatus();
        _keyboardStatusTimer.Start();
        UpdateKeyboardStatus();
    }

    private void OnLoginClicked(object? sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text?.Trim() ?? string.Empty;
        var password = PasswordTextBox.Text?.Trim() ?? string.Empty;

        try
        {
            var usersPath = Path.Combine(AppContext.BaseDirectory, "USERS.txt");
            var menuPath = Path.Combine(AppContext.BaseDirectory, "menu.txt");

            var authService = new AuthorizationService(usersPath);
            var statuses = authService.Authenticate(username, password);
            if (statuses == null)
            {
                ErrorTextBlock.Text = "Неверный логин или пароль.";
                return;
            }

            var menu = new DataMenu(menuPath);
            var mainWindow = new MainWindow(menu, statuses);
            mainWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = ex.Message;
        }
    }
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateKeyboardStatus()
    {
        InputLanguageTextBlock.Text = $"Язык ввода {GetInputLanguageName()}";
        CapsLockTextBlock.Text = $"Клавиша CapsLock {(IsCapsLockOn() ? "нажата" : "не нажата")}";
    }

    private static string GetInputLanguageName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsKeyboardLanguage();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacInputLanguage();
        }

        return GetMappedLanguageName(CultureInfo.CurrentCulture.DisplayName);
    }

    private static bool IsCapsLockOn()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (GetKeyState(0x14) & 1) != 0;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return CGEventSourceKeyState(0, 57);
        }

        return false;
    }

    private static string GetWindowsKeyboardLanguage()
    {
        var layout = GetKeyboardLayout(0);
        var langId = (ushort)(layout.ToInt64() & 0xFFFF);

        try
        {
            var culture = new CultureInfo(langId);
            return GetMappedLanguageName(culture.EnglishName);
        }
        catch
        {
            return "Неизвестно";
        }
    }

    private static string GetMacInputLanguage()
    {
        try
        {
            var keyStr = "AppleCurrentKeyboardLayoutInputSourceID";
            var appStr = "com.apple.HIToolbox";
            var encoding = CFStringEncoding.UTF8;

            var key = CFStringCreateWithCString(IntPtr.Zero, keyStr, encoding);
            if (key == IntPtr.Zero) return "Неизвестно";

            var appID = CFStringCreateWithCString(IntPtr.Zero, appStr, encoding);
            if (appID == IntPtr.Zero)
            {
                CFRelease(key);
                return "Неизвестно";
            }

            var value = CFPreferencesCopyAppValue(key, appID);
            var result = "Неизвестно";
            if (value != IntPtr.Zero)
            {
                var id = CFStringToString(value);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    if (id.Contains("Russian"))
                        result = "Русский";
                    else if (id.Contains("US") || id.Contains("British") || id.Contains("English"))
                        result = "Английский";
                    else if (id.Contains("Ukrainian"))
                        result = "Украинский";
                    else
                        result = "Неизвестно";
                }
                CFRelease(value);
            }

            CFRelease(key);
            CFRelease(appID);
            return result;
        }
        catch
        {
            return "Неизвестно";
        }
    }

    private static string GetMappedLanguageName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Неизвестно";
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("рус") || lower.Contains("rus") || lower.Contains("ru"))
        {
            return "Русский";
        }

        if (lower.Contains("англ") || lower.Contains("eng") || lower.Contains("us") || lower.Contains("en"))
        {
            return "Английский";
        }

        if (lower.Contains("укр"))
        {
            return "Украинский";
        }

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static string CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
        {
            return string.Empty;
        }

        var ptr = CFStringGetCStringPtr(cfString, CFStringEncoding.UTF8);
        if (ptr != IntPtr.Zero)
        {
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        var buffer = new StringBuilder(256);
        return CFStringGetCString(cfString, buffer, buffer.Capacity, CFStringEncoding.UTF8)
            ? buffer.ToString()
            : string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string str, CFStringEncoding encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringGetCStringPtr(IntPtr theString, CFStringEncoding encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern bool CFStringGetCString(IntPtr theString, StringBuilder buffer, int bufferSize, CFStringEncoding encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFPreferencesCopyAppValue(IntPtr key, IntPtr appID);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CGEventSourceKeyState(uint sourceStateID, byte keyCode);

    private enum CFStringEncoding : uint
    {
        UTF8 = 0x08000100
    }
}
