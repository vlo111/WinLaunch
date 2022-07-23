﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinLaunch
{
    partial class MainWindow : Window
    {
        #region Context menu
        private void miEdit_Click(object sender, RoutedEventArgs e)
        {
            SBItem Item = ((e.Source as MenuItem).DataContext as SBItem);

            RunEditExtension(Item);
        }

        private void miRemove_Click(object sender, RoutedEventArgs e)
        {
            SBItem Item = ((e.Source as MenuItem).DataContext as SBItem);

            SBM.RemoveItem(Item, true);
        }

        private void miOpen_Click(object sender, RoutedEventArgs e)
        {
            SBItem Item = ((e.Source as MenuItem).DataContext as SBItem);

            if (Item.IsFolder)
            {
                SBM.OpenFolder(Item);
            }
            else
            {
                ItemActivated(Item, EventArgs.Empty);
            }
        }

        private void miOpenAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            SBItem Item = ((e.Source as MenuItem).DataContext as SBItem);

            if (Item.IsFolder)
            {
                SBM.OpenFolder(Item);
            }
            else
            {
                ItemActivated(Item, EventArgs.Empty, true);
            }
        }

        private void miOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            SBItem Item = ((e.Source as MenuItem).DataContext as SBItem);

            if (Item.IsFolder)
                return;

            if (!Settings.CurrentSettings.DeskMode)
            {
                StartLaunchAnimations(Item);
                LaunchedItem = Item;
            }

            string filepath = Item.ApplicationPath;

            //open explorer and highlight the file 
            Process ExplorerProc = new Process();
            ExplorerProc.StartInfo.FileName = "explorer.exe";
            ExplorerProc.StartInfo.Arguments = "/select,\"" + filepath + "\"";
            ExplorerProc.Start();
        }
        #endregion

        #region Main Context Menu
        private void miAddFile_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.DereferenceLinks = false;

            if((bool)ofd.ShowDialog())
            {
                //add files 
                foreach (string fileName in ofd.FileNames)
                {
                    AddFile(fileName);
                }
            }
        }

        private void miAddFolder_Clicked(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog ofd = new System.Windows.Forms.FolderBrowserDialog();

            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AddFile(ofd.SelectedPath);
            }
        }

        private void miAddLink_Clicked(object sender, RoutedEventArgs e)
        {
            AddLink dialog = new AddLink();
            dialog.Owner = this;

            if((bool)dialog.ShowDialog())
            {
                AddFile(dialog.URL);
            }
        }

        private void miSettingsClicked(object sender, RoutedEventArgs e)
        {
            //open settings
            OpenSettingsDialog();
        }

        private void miQuitClicked(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(TranslationSource.Instance["CloseWarning"], TranslationSource.Instance["Quit"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                PerformItemBackup();

                MainWindow.WindowRef.Close();
                Environment.Exit(0);
            }
            
        }

        private void miTutorialClicked(object sender, RoutedEventArgs e)
        {
            //open url
            HideWinLaunch();
            MiscUtils.OpenURL("http://WinLaunch.org/howto.php");
        }
        #endregion


        #region MainCanvas events

        //patch events through
        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (FolderRenamingActive)
            {
                DeactivateFolderRenaming();
                return;
            }

            if (FadingOut)
                return;

            //if (e.MiddleButton == MouseButtonState.Pressed && Settings.CurrentSettings.MiddleMouseActivation == MiddleMouseButtonAction.Nothing)
            //{
            //    //show / hide toolbar
            //    ToggleToolbar();
            //}

            SBM.MouseDown(sender, e);
        }

        private void MainCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (FadingOut)
                return;

            SBM.MouseUp(sender, e);
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            if (FadingOut)
                return;

            SBM.MouseMove(sender, e);
        }

        private void MainCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            SBM.MouseLeave(sender, e);
        }

        private void MainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SBM != null)
            {
                SBM.UpdateDisplayRect(new Rect(0.0, 0.0, e.NewSize.Width, e.NewSize.Height));
                SBM.GM.SetGridPositions();

                if (SBM.FolderOpen)
                {
                    SBM.PositionFolderAndItems(false);
                }
            }

            this.FolderTitleGrid.Margin = new Thickness((180.5 / 1920.0) * MainCanvas.Width, (24.0 / 1080) * MainCanvas.Height, 0, 0);

            e.Handled = true;
        }

        #endregion MainCanvas events

        #region ItemActivated

        private SBItem LaunchedItem = null;

        //handles events from sbm
        public void ItemActivated(object sender, EventArgs e, bool RunAsAdmin = false)
        {
            try
            {
                if (FadingOut)
                    return;

                SBItem Item = sender as SBItem;

                if (EditExtensionActive)
                {
                    RunEditExtension(Item);
                }
                else
                {
                    //launch it
                    if (!Item.IsFolder)
                    {
                        try
                        {
                            if (System.IO.File.Exists(Item.ApplicationPath) || System.IO.Directory.Exists(Item.ApplicationPath) || Uri.IsWellFormedUriString(Item.ApplicationPath, UriKind.Absolute))
                            {
                                CleanMemory();

                                if (!Settings.CurrentSettings.DeskMode)
                                {
                                    StartLaunchAnimations(Item);
                                    LaunchedItem = Item;
                                }

                                new Thread(new ThreadStart(() =>
                                {
                                    try
                                    {
                                        ProcessStartInfo startInfo = new ProcessStartInfo();
                                        startInfo.UseShellExecute = true;
                                        startInfo.FileName = Item.ApplicationPath;
                                        startInfo.Arguments = Item.Arguments;

                                        if(Item.RunAsAdmin || RunAsAdmin)
                                        {
                                            startInfo.Verb = "runas";
                                        }
                                        
                                        startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(Item.ApplicationPath);
                                        Process.Start(startInfo);
                                    }
                                    catch(Exception ex) { }
                                })).Start();
                            }
                        }
                        catch //(Exception ex)
                        {
                            //MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex);
                MessageBox.Show(ex.Message);
            }
        }

        #endregion ItemActivated

        #region WindowEvents

        public static bool StartHidden = false;
        private bool LoadingAssets = true;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowRef = this;

            //Init DPI Scaling
            MiscUtils.GetDPIScale();

            //setup appdata directory
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch");
            if (!System.IO.Directory.Exists(appData))
            {
                System.IO.Directory.CreateDirectory(appData);
            }

            if (!System.IO.File.Exists(ItemCollection.CurrentItemsPath))
            {
                //Set autostart on first ever startup
                Autostart.SetAutoStart("WinLaunch", Assembly.GetExecutingAssembly().Location, " -hide");
            }

            //Load files
            LoadSettings();

            //initialize languages
            InitLocalization();

            //init springboard manager
            InitSBM();

            //begin loading theme
            BeginLoadTheme(() =>
            {
                //bitmaps loaded
                //begin loading icons
                BeginInitIC(() =>
                {
                    try
                    {
                        //all items loaded
                        //apply theme
                        InitTheme();
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.Report(ex);
                        MessageBox.Show(ex.Message);
                    }

                    ActivatorsEnabled = true;
                    LoadingAssets = false;
                });
            });

            InitSettings();

            #region hook up events

            //input events
            RegisterInputEvents();

            this.GotKeyboardFocus += MainWindow_GotKeyboardFocus;
            this.LostKeyboardFocus += new KeyboardFocusChangedEventHandler(MainWindow_LostKeyboardFocus);

            //canvas events
            this.MainCanvas.MouseDown += new MouseButtonEventHandler(MainCanvas_MouseDown);
            this.MainCanvas.MouseMove += new MouseEventHandler(MainCanvas_MouseMove);
            this.MainCanvas.MouseUp += new MouseButtonEventHandler(MainCanvas_MouseUp);
            this.MainCanvas.MouseLeave += new MouseEventHandler(MainCanvas_MouseLeave);
            this.MainCanvas.SizeChanged += new SizeChangedEventHandler(MainCanvas_SizeChanged);

            //window events
            this.DragEnter += new DragEventHandler(MainWindow_DragEnter);
            this.DragOver += new DragEventHandler(MainWindow_DragOver);
            this.Drop += new DragEventHandler(MainWindow_Drop);
            this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);

            this.FolderTitle.MouseDown += new MouseButtonEventHandler(FolderTitle_MouseDown);

            //framework events
            CompositionTargetEx.FrameUpdating += RenderFrame;

            //misc events
            WPWatch.WallpaperChanged += new EventHandler(WPWatch_Changed);
            WPWatch.BackgroundColorChanged += WPWatch_BackgroundColorChanged;
            WPWatch.AccentColorChanged += WPWatch_AccentColorChanged;
            #endregion hook up events

            //show if not hidden and on first ever startup
            if (!System.IO.File.Exists(ItemCollection.CurrentItemsPath) || !StartHidden)
            {
                //show window
                if (!Settings.CurrentSettings.DeskMode)
                {
                    Task.Factory.StartNew(() =>
                    {
                        //fix positioning bug
                        Thread.Sleep(100);
                    }).ContinueWith(t =>
                    {
                        RevealWindow();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
            else
            {
                if (!Settings.CurrentSettings.DeskMode)
                {
                    this.Visibility = System.Windows.Visibility.Hidden;
                    IsHidden = true;
                }
            }

            if (Settings.CurrentSettings.DeskMode)
            {
                Task.Factory.StartNew(() =>
                {
                    //fix positioning bug
                    Thread.Sleep(100);
                }).ContinueWith(t =>
                {
                    RevealWindow();
                    MakeDesktopChildWindow();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }


            
            e.Handled = true;
        }

        private static void InitLocalization()
        {
            try
            {
                TranslationSource.Instance.CurrentCulture = new CultureInfo(Settings.CurrentSettings.SelectedLanguage);
            }
            catch { }

            if (TranslationSource.Instance.CurrentCulture == null)
            {
                TranslationSource.Instance.CurrentCulture = new CultureInfo("en-US");
            }
        }

        private void MainWindow_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Activate();
            Focus();
        }

        private void MainWindow_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CleanMemory();
            //PerformItemBackup();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                hotCorner.Active = false;
            }
            catch { }
        }

        #endregion WindowEvents

        #region Activators
        private bool ActivatorsEnabled = false;

        #region HotCornerActivator
        private HotCorner hotCorner = null;

        private void InitHotCornerActivator()
        {
            hotCorner = new HotCorner();

            HotCorner.Corners corners = HotCorner.Corners.None;
            corners |= Settings.CurrentSettings.HotTopLeft ? HotCorner.Corners.TopLeft : HotCorner.Corners.None;
            corners |= Settings.CurrentSettings.HotTopRight ? HotCorner.Corners.TopRight : HotCorner.Corners.None;
            corners |= Settings.CurrentSettings.HotBottomRight ? HotCorner.Corners.BottomRight : HotCorner.Corners.None;
            corners |= Settings.CurrentSettings.HotBottomLeft ? HotCorner.Corners.BottomLeft : HotCorner.Corners.None;

            hotCorner.SetCorners(corners);

            hotCorner.Activated += new EventHandler<HotCornerArgs>(hotCorner_Activated);

            if (!Settings.CurrentSettings.HotCornersEnabled)
                hotCorner.Active = false;
            else
                hotCorner.Active = true;
        }

        private void hotCorner_Activated(object sender, HotCornerArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (!ActivatorsEnabled)
                    return;

                ToggleLaunchpad();
            }));
        }
        #endregion HotCornerActivator

        #region MiddleMouseButtonActivator
        MiddleMouseActivation middleMouseActivator = null;
        DoubleClickEvent middleMouseDoubleClick = null;

        void InitMiddleMouseButtonActivator()
        {
            middleMouseDoubleClick = new DoubleClickEvent();
            middleMouseDoubleClick.DoubleClicked += middleMouseDoubleClick_DoubleClicked;

            middleMouseActivator = new MiddleMouseActivation();
            middleMouseActivator.Activated += middleMouseActivator_Activated;

            //skip in debug due to cursor jitter
#if !DEBUG
            middleMouseActivator.Begin();
#endif
        }

        void middleMouseActivator_Activated(object sender, MiddleMouseButtonActivatedEventArgs e)
        {
            if (!ActivatorsEnabled)
                return;

            if (Settings.CurrentSettings.MiddleMouseActivation == MiddleMouseButtonAction.Nothing)
                return;

            if (Settings.CurrentSettings.MiddleMouseActivation == MiddleMouseButtonAction.DoubleClicked)
            {
                if (middleMouseDoubleClick.Click())
                {
                    e.handled = true;
                }

                return;
            }

            //this will block the middle mouse button input systemwide
            e.handled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ToggleLaunchpad();
            }));
        }

        void middleMouseDoubleClick_DoubleClicked(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ToggleLaunchpad();
            }));
        }
        #endregion

        #region ShortcutActivator
        private ShortcutActivation shortcutActivator = null;

        private void InitShortcutActivator()
        {
            shortcutActivator = new ShortcutActivation();
            shortcutActivator.InitListener((HwndSource)HwndSource.FromVisual(this));

            shortcutActivator.Activated += new EventHandler(shortcutActivator_Activated);
        }

        private void shortcutActivator_Activated(object sender, EventArgs e)
        {
            if (!ActivatorsEnabled)
                return;

            ToggleLaunchpad();
        }
        #endregion ShortcutActivator

        #region Hotkey
        private HotKey hotkey = null;

        private void InitHotKey()
        {
            //init configurable hotkey
            hotkey = new HotKey((HwndSource)HwndSource.FromVisual(this));

            hotkey.Modifiers |= (Settings.CurrentSettings.HotAlt ? HotKey.ModifierKeys.Alt : 0);
            hotkey.Modifiers |= (Settings.CurrentSettings.HotControl ? HotKey.ModifierKeys.Control : 0);
            hotkey.Modifiers |= (Settings.CurrentSettings.HotShift ? HotKey.ModifierKeys.Shift : 0);
            hotkey.Modifiers |= (Settings.CurrentSettings.HotWin ? HotKey.ModifierKeys.Win : 0);

            hotkey.Key = Settings.CurrentSettings.HotKeyExtend;

            hotkey.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyDown);

            try
            {
                if (Settings.CurrentSettings.HotKeyEnabled)
                    hotkey.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("could not enable hotkey" + ex.Message, "Winlaunch Error");
            }
        }

        private void PanicHotKeyDown(object sender, HotKeyEventArgs e)
        {
            //panic hotkey always shows the window
            RevealWindow();
            MakeDesktopWindow();
        }

        private void HotKeyDown(object sender, HotKeyEventArgs e)
        {
            if (!ActivatorsEnabled)
                return;

            ToggleLaunchpad();
        }
        #endregion Hotkey

        #endregion Activators

        #region Utils
        //gets called whenever a backup should be performed
        public void PerformItemBackup()
        {
            if (LoadingAssets)
                return;

            try
            {
                SBM.IC.SaveToXML(ItemCollection.CurrentItemsPath);
                backupManager.AddBackup(ItemCollection.CurrentItemsPath);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not save items" + e.Message);
            }
        }

        private void AddFile(string File)
        {
            try
            {
                BitmapSource bmps;
                string Name;
                string Path;

                if (Uri.IsWellFormedUriString(File, UriKind.Absolute))
                {
                    //link
                    Name = File;
                    Path = File;

                    if (Name == "")
                        return;

                    bmps = MiscUtils.GetFileThumbnail(File);
                }
                else if ((System.IO.File.GetAttributes(File) & System.IO.FileAttributes.Directory) == System.IO.FileAttributes.Directory)
                {
                    //folder
                    string folder = File;

                    Name = folder.Substring(folder.LastIndexOf('\\') + 1);
                    Path = folder;

                    if (Name == "")
                        return;

                    bmps = MiscUtils.GetFileThumbnail(folder);
                }
                else
                {
                    //file
                    string file = File;
                    string Extension = System.IO.Path.GetExtension(file).ToLower();

                    Name = System.IO.Path.GetFileNameWithoutExtension(File);
                    Path = file;

                    //cache lnk files
                    if (Extension == ".lnk")
                    {
                        string cacheDir = System.IO.Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch", "LinkCache" });

                        if (!Directory.Exists(cacheDir))
                        {
                            Directory.CreateDirectory(cacheDir);
                        }

                        string guid = Guid.NewGuid().ToString();
                        string cacheFile = System.IO.Path.Combine(cacheDir, guid + ".lnk");

                        System.IO.File.Copy(file, cacheFile);

                        Path = cacheFile;
                    }

                    if (Name == "")
                        return;

                    bmps = MiscUtils.GetFileThumbnail(File);
                }

                SBM.AddItem(new SBItem(Name, Path, null, "", bmps), (int)SBM.SP.CurrentPage, -1);
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex);
                MessageBox.Show(ex.Message);
            }
        }

        #endregion Utils

        #region Input

        private void UnregisterInputEvents()
        {
            //unregister input events
            this.MouseWheel -= MainWindow_MouseWheel;

            this.KeyDown -= MainWindow_KeyDown;
            this.KeyUp -= MainWindow_KeyUp;
        }

        private void RegisterInputEvents()
        {
            this.MouseWheel += new MouseWheelEventHandler(MainWindow_MouseWheel);

            this.KeyDown += new KeyEventHandler(MainWindow_KeyDown);
            this.KeyUp += new KeyEventHandler(MainWindow_KeyUp);
        }


        #region Input events
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //Check MouseWheel
            if (e.Delta != 0)
            {
                if (!(this.Visibility == System.Windows.Visibility.Visible) || !ActivatorsEnabled)
                    return;

                if (e.Delta == 120)
                {
                    SBM.SP.FlipPageLeft();
                }
                else if (e.Delta == -120)
                {
                    SBM.SP.FlipPageRight(SBM.JiggleMode);
                }
            }

            e.Handled = true;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (FolderRenamingActive)
                return;

            e.Handled = false;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (FolderRenamingActive)
                return;

            if (e.Key == Key.Escape)
            {
                ToggleLaunchpad();

                return;
            }

            //using WinForms because WPF System.Windows.Input.Mouse is not system wide
            if (e.Key == Key.F && System.Windows.Forms.Control.MouseButtons == System.Windows.Forms.MouseButtons.None)
            {
                //if (IsFullscreen)
                //{
                //    //fullscreen maximized -> make desktop window
                //    MakeDesktopWindow();
                //}
                //else
                //{
                //    //desktop window -> make fullscreen
                //    if (Settings.CurrentSettings.DeskMode)
                //    {
                //        MakeDesktopChildWindow();
                //    }
                //    else
                //    {
                //        HideWindow();
                //        RevealWindow();
                //    }
                //}

                miAddFile_Clicked(this, null);
            }

            SBM.KeyDown(sender, e);

            e.Handled = true;
        }
        #endregion
        #endregion Input

        #region DragnDrop
        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            string[] FileList = (string[])e.Data.GetData(DataFormats.FileDrop, true);
            foreach (string File in FileList)
            {
                AddFile(File);
            }

            if (!IsDesktopChild)
                Keyboard.ClearFocus();
        }

        #endregion

        #region Background Theme Events

        private void WPWatch_Changed(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if(!Theme.CurrentTheme.UseAeroBlur && !Theme.CurrentTheme.UseCustomBackground)
                {
                    SetSyncedTheme();
                }
            }));
        }

        private void WPWatch_BackgroundColorChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                UpdateBackgroundColors();
            }));
        }

        private void WPWatch_AccentColorChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                UpdateBackgroundColors();
            }));
        }
        #endregion
    }
}