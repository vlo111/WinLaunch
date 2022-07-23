﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WinLaunch
{
    /// <summary>
    /// Interaktionslogik für InstantSettings.xaml
    /// </summary>
    public partial class InstantSettings : Window
    {
        public MainWindow mainWindow { get; set; }

        public Settings settings { get; set; }
        public Theme theme { get; set; }

        DelayedAction UpdateBackgroundBlurAction = new DelayedAction();

        public InstantSettings(MainWindow mainWindow)
        {
            settings = Settings.CurrentSettings;
            theme = Theme.CurrentTheme;
            this.mainWindow = mainWindow;

            DataContext = this;

            InitializeComponent();

            Loaded += InstantSettings_Loaded;
            Closing += InstantSettings_Closing;
        }

        void InstantSettings_Loaded(object sender, RoutedEventArgs e)
        {
            InitSettings();

            //attach events
            //general
            cbFillScreen.Checked += UpdateWindow;
            cbFillScreen.Unchecked += UpdateWindow;

            cbDeskMode.Checked += CbDeskMode_Checked;
            cbDeskMode.Unchecked += CbDeskMode_Checked;

            //multi screen
            cbScreens.SelectionChanged += cbScreens_SelectionChanged;

            //Icons
            slIconSize.ValueChanged += UpdateIconSize;
            slIconShadowOpacity.ValueChanged += UpdateIconShadows;

            cpIconTextColor.SelectedColorChanged += UpdateIconColors;
            cpIconTextShadowColor.SelectedColorChanged += UpdateIconColors;

            //columns & rows
            slFolderColumnsSlider.ValueChanged += UpdateFolders;

            //Background
            cbEnableAero.Checked += UpdateBackground;
            cbEnableAero.Unchecked += UpdateBackground;

            cbEnableAcrylic.Checked += UpdateBackground;
            cbEnableAcrylic.Unchecked += UpdateBackground;

            slBackgroundTint.ValueChanged += UpdateBackgroundTint;
            slBlurRadius.ValueChanged += UpdateBackgroundBlur;

            cbEnableCustomBackground.Checked += UpdateCustomBackground;
            cbEnableCustomBackground.Unchecked += UpdateBackground;
        }

        private void tbTranslationLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            tbTranslationLink.Text = "http://winlaunch.org/translate.php (" + TranslationSource.Instance["Copied"] + ")";

            Clipboard.SetText("http://winlaunch.org/translate.php");
        }

        private void InitDeskMode()
        {
            cbDeskMode.IsChecked = settings.DeskMode;
        }

        private void CbDeskMode_Checked(object sender, RoutedEventArgs e)
        {
            if (Settings.CurrentSettings.DeskMode == (bool)cbDeskMode.IsChecked)
            {
                //no change 
                return;
            }

            //we have to restart the application to switch deskmode
            if (MessageBox.Show(TranslationSource.Instance["DeskModeSwitch"], "Switch DeskMode", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
            {
                //save settings and restart 
                Settings.CurrentSettings.DeskMode = (bool)cbDeskMode.IsChecked;

                Settings.SaveSettings(Settings.CurrentSettingsPath, Settings.CurrentSettings);

                //restart
                MiscUtils.RestartApplication();
            }
            else
            {
                InitDeskMode();
            }
        }

        void InstantSettings_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ApplySettings();

            //save settings
            Settings.SaveSettings(Settings.CurrentSettingsPath, Settings.CurrentSettings);
            Theme.SaveTheme(Theme.CurrentTheme);
        }

        void InitSettings()
        {
            InitLocalization();
            InitAutostart();
            InitScreenSelection();
            InitHotkey();
            InitDeskMode();
            InitAero();

            tbVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        void ApplySettings()
        {
            ApplyAutostart();
            ApplyColumnsSetting();
        }

        #region General
        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        void InitAutostart()
        {
            try
            {
                cbAutostart.IsChecked = Autostart.IsAutoStartEnabled("WinLaunch", Assembly.GetExecutingAssembly().Location, " -hide");
            }
            catch
            {
                //cant access registry -> disable option
                cbAutostart.IsEnabled = false;
            }
        }

        void ApplyAutostart()
        {
            if ((bool)cbAutostart.IsChecked)
            {
                Autostart.SetAutoStart("WinLaunch", Assembly.GetExecutingAssembly().Location, " -hide");
            }
            else
            {
                if (Autostart.IsAutoStartEnabled("WinLaunch", Assembly.GetExecutingAssembly().Location, " -hide"))
                    Autostart.UnsetAutoStart("WinLaunch");
            }
        }

        void UpdateWindow(object sender, RoutedEventArgs e)
        {
            mainWindow.UpdateWindowPosition();
        }
        #endregion

        #region Localization
        List<CultureInfo> cultures = TranslationSource.GetAvailableCultures().ToList();

        public void InitLocalization()
        {
            foreach (CultureInfo culture in cultures)
            {
                cbbLanguages.Items.Add(culture.DisplayName);
            }

            //select current culture
            cbbLanguages.SelectedIndex = cultures.IndexOf(TranslationSource.Instance.CurrentCulture);
        }

        private void cbbLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //update language settings
            settings.SelectedLanguage = cultures[cbbLanguages.SelectedIndex].Name;
            TranslationSource.Instance.CurrentCulture = cultures[cbbLanguages.SelectedIndex];
        }
        #endregion

        #region Multi monitors
        void InitScreenSelection()
        {
            System.Windows.Forms.Screen[] Screens = System.Windows.Forms.Screen.AllScreens;
            int ScreenCount = Screens.GetUpperBound(0) + 1;
            int ScreenIndex = settings.ScreenIndex;

            if (ScreenIndex > ScreenCount - 1)
                ScreenIndex = ScreenCount - 1;

            cbScreens.Items.Clear();

            //add items to the combobox
            for (int i = 0; i < ScreenCount; i++)
            {
                ComboBoxItem Entry = new ComboBoxItem();
                Entry.Content = TranslationSource.Instance["Screen"] + " " + (i + 1).ToString();

                cbScreens.Items.Add(Entry);
            }

            if (!settings.OpenOnActiveDesktop)
                cbScreens.SelectedIndex = ScreenIndex;
        }

        private void cbScreens_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbScreens.SelectedIndex != -1)
            {
                //update screen
                settings.ScreenIndex = cbScreens.SelectedIndex;

                mainWindow.UpdateWindowPosition();
            }
        }
        #endregion

        #region Hotkey
        bool waitingForHotkey = false;

        private void InitHotkey()
        {
            btnHotKey.Content = settings.HotKeyExtend;
        }

        private void btnHotKey_Click(object sender, RoutedEventArgs e)
        {
            if (!waitingForHotkey)
            {
                //listen for hotkey event 
                btnHotKey.Content = TranslationSource.Instance["PressAKey"];
                waitingForHotkey = true;
            }
        }

        private void btnHotKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (waitingForHotkey)
            {
                e.Handled = true;

                waitingForHotkey = false;
                btnHotKey.Content = e.Key;
                settings.HotKeyExtend = e.Key;
            }
        }
        #endregion

        #region Columns & rows
        void UpdateFolders(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mainWindow.SBM.UpdateFolderPosition();
        }

        void ApplyColumnsSetting()
        {
            theme.Columns = (int)slColumnsSlider.Value;
            theme.Rows = (int)slRowsSlider.Value;
            theme.FolderColumns = (int)slFolderColumnsSlider.Value;
        }
        #endregion

        #region Icons
        private void UpdateIconSize(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //update ui
            mainWindow.SBM.UpdateIcons();
        }

        private void UpdateIconShadows(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //update ui
            mainWindow.SBM.UpdateIcons();
        }

        private void UpdateIconColors(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            //update ui
            mainWindow.SBM.UpdateIcons();
        }
        #endregion

        #region Background
        void InitAero()
        {
            spAero.IsEnabled = GlassUtils.IsBlurBehindAvailable() && !settings.DeskMode;

            if (!spAero.IsEnabled)
            {
                theme.UseAeroBlur = false;
            }

            spBackgroundImageOptions.IsEnabled = !theme.UseAeroBlur;
        }

        void UpdateBackground(object sender, RoutedEventArgs e)
        {
            if (theme.UseAeroBlur && !mainWindow.AllowsTransparency)
            {
                //we need to restart to switch
                if (MessageBox.Show(TranslationSource.Instance["AeroSwitch"], TranslationSource.Instance["EnableAero"], MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                {
                    //save theme and restart 
                    Theme.SaveTheme(Theme.CurrentTheme);

                    //restart
                    MiscUtils.RestartApplication();
                }
                else
                {
                    //clicked no
                    theme.UseAeroBlur = false;
                    BindingOperations.GetBindingExpression(cbEnableAero, CheckBox.IsCheckedProperty).UpdateTarget();
                }
            }
            else
            {
                cbEnableAcrylic.IsEnabled = theme.UseAeroBlur;
                slBackgroundTint.IsEnabled = theme.UseAeroBlur;
                spBackgroundImageOptions.IsEnabled = !theme.UseAeroBlur;

                mainWindow.SetupThemes();
            }
        }

        void UpdateBackgroundTint(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mainWindow.UpdateBackgroundColors();
        }

        void UpdateBackgroundBlur(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateBackgroundBlurAction.RunIn(400, () =>
            {
                UpdateBackgroundBlur();
            });
        }

        void UpdateBackgroundBlur()
        {
            slBlurRadius.IsEnabled = false;

            //force reblur
            theme.BlurredBackground = null;

            mainWindow.ForceBackgroundUpdate(() =>
            {
                slBlurRadius.IsEnabled = true;
            });
        }

        private void UpdateCustomBackground(object sender, RoutedEventArgs e)
        {
            try
            {
                //load new background image
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Multiselect = false;
                ofd.Filter = "Images (*.png,*.jpg,*.jpeg)|*.png;*.jpg;*.jpeg";

                if ((bool)ofd.ShowDialog())
                {
                    //add files 
                    theme.Background = MiscUtils.LoadBitmapImage(ofd.FileName);

                    //save it to theme folder
                    theme.SaveBackground();

                    //update background
                    mainWindow.SetupThemes();

                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            theme.UseCustomBackground = false;
            BindingOperations.GetBindingExpression(cbEnableCustomBackground, CheckBox.IsCheckedProperty).UpdateTarget();
        }

        #endregion

        #region About
        private void tbEmailLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            tbEmailLink.Text = "winlaunch.official@gmail.com (" + TranslationSource.Instance["Copied"] + ")";

            Clipboard.SetText("winlaunch.official@gmail.com");
        }

        private void btnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UpdateCheck.Run())
                {
                    MessageBox.Show(TranslationSource.Instance["LatestVersion"], "", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(TranslationSource.Instance["UpdateError"], "Winlaunch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Backup and Restore
        private void btnSaveBackup_Click(object sender, RoutedEventArgs e)
        {
            string settingsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch");

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "WinLaunch Backup|*.WLbackup";
            sfd.FileName = "WinLaunch." + DateTime.Now.ToString("MM.dd.yyyy");
            sfd.OverwritePrompt = true;

            if ((bool)sfd.ShowDialog())
            {
                try
                {
                    //save current settings
                    Settings.SaveSettings(Settings.CurrentSettingsPath, settings);

                    if (File.Exists(sfd.FileName))
                    {
                        File.Delete(sfd.FileName);
                    }

                    ZipFile.CreateFromDirectory(settingsDir, sfd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating backup " + ex.Message, "WinLaunch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("Backup saved to " + sfd.FileName, "Backup created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnLoadBackup_Click(object sender, RoutedEventArgs e)
        {
            string settingsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch");

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "WinLaunch Backup|*.WLbackup";

            if ((bool)ofd.ShowDialog())
            {
                if (MessageBox.Show("Restoring from this backup will remove the current configuration, are you sure you want to restore?", "Restore Backup", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        //delete folder contents
                        Directory.Delete(settingsDir, true);

                        //unzip into it
                        ZipFile.ExtractToDirectory(ofd.FileName, settingsDir);

                        //restart winlaunch
                        MiscUtils.RestartApplication();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error restoring from backup " + ex.Message, "WinLaunch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        } 
        #endregion
    }
}
