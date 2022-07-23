﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinLaunch
{
    public class ScreenRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public ScreenRect(System.Drawing.Rectangle r)
        {
            X = r.X;
            Y = r.Y;
            Width = r.Width;
            Height = r.Height;
        }
    }

    partial class MainWindow : Window
    {
        #region Disable close button

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_ENABLED = 0x00000000;

        private const uint SC_CLOSE = 0xF060;

        private const int WM_SHOWWINDOW = 0x00000018;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;

            if (hwndSource != null)
            {
                hwndSource.AddHook(new HwndSourceHook(this.hwndSourceHook));
            }
        }

        private IntPtr hwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SHOWWINDOW)
            {
                IntPtr hMenu = GetSystemMenu(hwnd, false);
                if (hMenu != IntPtr.Zero)
                {
                    EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
                }
            }

            return IntPtr.Zero;
        }

        #endregion Disable close button

        private bool IsFullscreen = true;
        private bool IsHidden = false;

        private double GetOptimalResolutionSettings(double Width, double Height)
        {
            //768 - 0.79
            //1080 - 1.0
            if (Height >= 1080 - 40)
                return 1.0;

            if (Height < 768 - 40)
                return 0.7;

            double resolution_value = (Height - (768 - 40)) / ((1080 - 40) - (768 - 40));
            double scale = 0.79 + (1.0 - 0.79) * resolution_value;

            return scale;
        }

        private double CurrentScale = 1.0;

        private void ChangeResolution(double Left, double Top, double Width, double Height, double scale)
        {
            this.Width = Width;
            this.Height = Height;
            this.Left = Left;
            this.Top = Top;

            this.CurrentScale = scale;

            //set rendersize
            this.MainCanvas.Width = this.Width * (1.0 / scale);
            this.MainCanvas.Height = this.Height * (1.0 / scale);

            //set presentersize
            this.CanvasScale.ScaleX = scale;
            this.CanvasScale.ScaleY = scale;
        }

        //TODO: dpi
        private Rect GetDeskModeRect()
        {
            double DPIscale = MiscUtils.GetDPIScale();

            //desktop coordinate system always starts at 0, 0
            //convert the (random) screen coordinates to desktop coordinates

            //normalize desktop bounds
            List<ScreenRect> Bounds = new List<ScreenRect>();

            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;

            foreach (var screen in screens)
            {
                Bounds.Add(new ScreenRect(screen.WorkingArea));
            }

            //normalize the screen
            double minX = 0;
            double minY = 0;

            foreach (var screen in Bounds)
            {
                if (screen.X < minX)
                {
                    minX = screen.X;
                }

                if (screen.Y < minY)
                {
                    minY = screen.Y;
                }
            }

            //transform screens to desktop space
            for (int i = 0; i < Bounds.Count; i++)
            {
                Bounds[i].X -= minX;
                Bounds[i].Y -= minY;
            }

            return new Rect(Bounds[Settings.CurrentSettings.ScreenIndex].X,
                            Bounds[Settings.CurrentSettings.ScreenIndex].Y,
                            Bounds[Settings.CurrentSettings.ScreenIndex].Width,
                            Bounds[Settings.CurrentSettings.ScreenIndex].Height);
        }

        private Rect GetFullscreenRect()
        {
            double Left = 0;
            double Top = 0;
            double Width = 0;
            double Height = 0;

            #region DPI aware

            double DPIscale = MiscUtils.GetDPIScale();

            #endregion DPI aware

            #region Multi-Screen

            int ScreenIndex;

            if (Settings.CurrentSettings.OpenOnActiveDesktop && !Settings.CurrentSettings.DeskMode)
            {
                ScreenIndex = MiscUtils.GetActiveScreenIndex();
            }
            else
            {
                //fixed screen or desk mode
                ScreenIndex = Settings.CurrentSettings.ScreenIndex;
            }

            if (ScreenIndex > System.Windows.Forms.Screen.AllScreens.GetUpperBound(0))
                ScreenIndex = 0;

            System.Windows.Forms.Screen Screen = System.Windows.Forms.Screen.AllScreens[ScreenIndex];

            //select primary screen for desk mode for now
            if (Settings.CurrentSettings.DeskMode)
            {
                Screen = System.Windows.Forms.Screen.PrimaryScreen;
            }

            if (Settings.CurrentSettings.FillScreen && !Settings.CurrentSettings.DeskMode)
            {
                Left = (double)Screen.Bounds.Left;
                Top = (double)Screen.Bounds.Top;
                Width = (double)Screen.Bounds.Width;
                Height = (double)Screen.Bounds.Height;
            }
            else
            {
                Left = (double)Screen.WorkingArea.Left;
                Top = (double)Screen.WorkingArea.Top;
                Width = (double)Screen.WorkingArea.Width;
                Height = (double)Screen.WorkingArea.Height;
            }

            #endregion Multi-Screen

            return new Rect(Left / DPIscale, Top / DPIscale, Width / DPIscale, Height / DPIscale);
        }

        #region DeskMode

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpWindowClass, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("User32", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndParent);

        private IntPtr OriginalParentWindow = IntPtr.Zero;
        private bool IsDesktopChild = false;

        private void MakeDesktopChildWindow()
        {
            if (IsDesktopChild)
                return;

            this.ShowInTaskbar = false;
            this.Topmost = false;
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;

            Rect FullScreenRect = GetDeskModeRect();

            ChangeResolution(FullScreenRect.X, FullScreenRect.Y,
                FullScreenRect.Width,
                FullScreenRect.Height,
                1.0
                );

            IntPtr hprog = FindWindowEx(
                FindWindowEx(
                    FindWindow("Progman", "Program Manager"),
                    IntPtr.Zero, "SHELLDLL_DefView", ""
                ),
                IntPtr.Zero, "SysListView32", "FolderView"
            );

            IntPtr WorkerW = IntPtr.Zero;
            while (hprog == IntPtr.Zero)
            {
                WorkerW = FindWindowEx(IntPtr.Zero, WorkerW, "WorkerW", "");

                if (WorkerW == IntPtr.Zero)
                    break;

                IntPtr ShellDll = FindWindowEx(WorkerW, IntPtr.Zero, "SHELLDLL_DefView", "");
                hprog = FindWindowEx(ShellDll, IntPtr.Zero, "SysListView32", "FolderView");
            }

            if (hprog == IntPtr.Zero)
            {
                MessageBox.Show("failed to locate the desktop!", "Error");
            }

            IntPtr windowHandle = new WindowInteropHelper(this).Handle;

            OriginalParentWindow = SetParent(windowHandle, hprog);

            IsDesktopChild = true;
            IsFullscreen = true;
        }

        private void UnsetDesktopChild()
        {
            if (IsDesktopChild)
            {
                IntPtr windowHandle = new WindowInteropHelper(this).Handle;
                SetParent(windowHandle, OriginalParentWindow);

                IsDesktopChild = false;
            }
        }

        #endregion DeskMode

        /// <summary>
        /// updates the window position based on the current settings (deskmode, screen placement, etc.)
        /// </summary>
        public void UpdateWindowPosition()
        {
            if (Settings.CurrentSettings.DeskMode)
            {
                if (!IsDesktopChild)
                {
                    MakeDesktopChildWindow();
                }
                else
                {
                    //switch screens
                    Rect FullScreenRect = GetDeskModeRect();

                    //strange bug -> call twice
                    ChangeResolution(FullScreenRect.X, FullScreenRect.Y,
                        FullScreenRect.Width,
                        FullScreenRect.Height,
                        1.0
                        );

                    Debug.WriteLine(this.Left);

                    ChangeResolution(FullScreenRect.X, FullScreenRect.Y,
                        FullScreenRect.Width,
                        FullScreenRect.Height,
                        1.0
                        );

                    Debug.WriteLine(this.Left);
                }
            }
            else
            {
                MakeFullscreen();
            }
        }

        public void MakeFullscreen()
        {
            this.ShowInTaskbar = false;

            if (IsDesktopChild)
                UnsetDesktopChild();

            this.WindowStyle = System.Windows.WindowStyle.None;

            Rect FullScreenRect = GetFullscreenRect();

            ChangeResolution(FullScreenRect.X, FullScreenRect.Y,
                FullScreenRect.Width,
                FullScreenRect.Height,
                1.0
                );

            this.ResizeMode = System.Windows.ResizeMode.NoResize;
            IsFullscreen = true;

            if (!this.Topmost)
                this.Topmost = true;
        }

        private void MakeDesktopWindow()
        {
            this.ShowInTaskbar = true;

            if (IsDesktopChild)
                UnsetDesktopChild();

            this.ResizeMode = System.Windows.ResizeMode.NoResize;
            this.WindowStyle = System.Windows.WindowStyle.ToolWindow;

            Rect FullScreenRect = GetFullscreenRect();

            Rect CenterRect = MiscUtils.RectCenterInScreen(FullScreenRect.Width * 0.5, FullScreenRect.Height * 0.5 + 30, Settings.CurrentSettings.ScreenIndex);

            ChangeResolution(CenterRect.X, CenterRect.Y,
                CenterRect.Width,
                CenterRect.Height,
                0.5);

            IsFullscreen = false;
        }

        private void RevealWindow()
        {
            this.Visibility = System.Windows.Visibility.Visible;
            MakeFullscreen();

            IsHidden = false;

            StartFlyInAnimation();
        }

        private void HideWindow()
        {
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.Hide();
            IsHidden = true;
        }

        public void ToggleLaunchpad()
        {
            if (MainContextMenu.IsLoaded)
                return;

            if (extensionBarVisible)
            {
                ToggleToolbar();
                return;
            }
                

            CleanMemory();

            //hide if shown and show if hidden
            if (this.Visibility == System.Windows.Visibility.Hidden)
            {
                //dont show if another app is running in fullscreen
                if (Settings.CurrentSettings.BlockOnFullscreen && FullscreenAppDetector.Detected(this))
                    return;

                RevealWindow();
            }
            else
            {
                //dont close while moving or fading out
                if (FadingOut || SBM.Moving || SBM.SP.Scrolling || SBM.JiggleModeAttempt || SBM.MoveItemAttempt)
                    return;

                //if jiggle mode is on -> stop it
                if (SBM.JiggleMode)
                {
                    SBM.StopMoveMode();
                    return;
                }

                //if folder is open close it
                if (SBM.FolderOpen)
                {
                    SBM.BeginCloseFolder();
                    return;
                }

                if (Settings.CurrentSettings.DeskMode)
                    return;

                //start fade out animation
                //Animation -> HideWinLaunch -> HideWindow
                StartFlyOutAnimation();
            }
        }

        private void ForceFadeOut()
        {
            //start fade out animation
            //Animation -> HideWinLaunch -> HideWindow
            StartFlyOutAnimation();
        }

        //should be called after the window is invisible
        private void HideWinLaunch()
        {
            //TODO: better tutorial
            //show tutorial when winlaunch closes for the first time
            if (!Settings.CurrentSettings.PerformanceAdvisorShown)
            {
                MiscUtils.OpenURL("http://WinLaunch.org/howto.php");

                Settings.CurrentSettings.PerformanceAdvisorShown = true;
                Settings.SaveSettings(Settings.CurrentSettingsPath, Settings.CurrentSettings);
            }

            HideToolbar();

            HideWindow();

            SBM.StopMoveMode();

            if (SBM.FolderOpen)
            {
                SBM.CloseFolderInstant();
                CloseFolderGrid();
            }

            if (StartingItem)
            {
                LaunchedItem.ScaleAnim.Value = 1.0;
                LaunchedItem = null;
            }

            FadingOut = false;
            StartingItem = false;

            //save items
            PerformItemBackup();
        }
    }
}