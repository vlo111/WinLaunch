﻿using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WinLaunch
{
    /// <summary>
    /// Interaktionslogik für RenameItem.xaml
    /// </summary>
    partial class EditItem : Window
    {
        #region Properties

        private double PreferredPointerOffset = 110;

        private SBItem ActiveItem;
        private MainWindow ClientWindow;

        #endregion Properties

        #region item properties backup

        private BitmapSource IconBackup;
        private string ApplicationPathBackup;
        private string NameBackup;
        private string IconPathBackup;
        private string ArgumentsBackup;
        private bool RunAsAdminBackup;

        private void PerformBackup(SBItem Item)
        {
            IconBackup = Item.Icon;
            ApplicationPathBackup = Item.ApplicationPath;
            NameBackup = Item.Name;
            IconPathBackup = Item.IconPath;
            ArgumentsBackup = Item.Arguments;
            RunAsAdminBackup = Item.RunAsAdmin;
        }

        private void RestoreBackup(SBItem Item)
        {
            Item.Icon = IconBackup;
            Item.ApplicationPath = ApplicationPathBackup;
            Item.Name = NameBackup;
            Item.IconPath = IconPathBackup;
            Item.Arguments = ArgumentsBackup;
            Item.RunAsAdmin = RunAsAdminBackup;

            Item.UpdateIcon();
        }

        #endregion item properties backup

        private void PositionWindowAtItem(SBItem Item, MainWindow hWnd)
        {
            //Get local item position
            Point ItemOnScreen = Item.GetPosition();
            ItemOnScreen.X += Item.GetOffset().X;
            ItemOnScreen.X += Item.ContentRef.ActualWidth / 2.0;
            ItemOnScreen.Y += Item.ContentRef.ActualHeight / 2.0;

            double scale = hWnd.CanvasScale.ScaleX;
            ItemOnScreen.X *= scale;
            ItemOnScreen.Y *= scale;

            if (hWnd.WindowStyle != System.Windows.WindowStyle.None)
            {
                //add window borders
                //TODO: style independent
                ItemOnScreen.X += 2.0;
                ItemOnScreen.Y += 20.0;
            }

            ItemOnScreen.X += hWnd.Left;
            ItemOnScreen.Y += hWnd.Top;

            //Initialize pointer
            SetPointerPosition(PreferredPointerOffset, true);

            System.Drawing.Rectangle s;
            int screenIndex = MiscUtils.GetScreenIndexFromPoint(new System.Drawing.Point((int)ItemOnScreen.X, (int)ItemOnScreen.Y));

            if(screenIndex == -1)
            {
                //center on active screen
                //center window and hide pointer
                SetPointerPosition(PreferredPointerOffset, false);
                MiscUtils.CenterInScreen(this, MiscUtils.GetActiveScreenIndex());

                return;
            }
            else
            {
                //check if we are in bounds
                System.Drawing.Rectangle sDPI = System.Windows.Forms.Screen.AllScreens[screenIndex].Bounds;

                s = new System.Drawing.Rectangle((int)(sDPI.Left / MiscUtils.GetDPIScale()),(int)(sDPI.Top / MiscUtils.GetDPIScale()), (int)(sDPI.Width / MiscUtils.GetDPIScale()), (int)(sDPI.Height / MiscUtils.GetDPIScale()));

                if (!s.Contains((int)ItemOnScreen.X, (int)ItemOnScreen.Y))
                {
                    //center on active screen
                    //center window and hide pointer
                    SetPointerPosition(PreferredPointerOffset, false);
                    MiscUtils.CenterInScreen(this, MiscUtils.GetActiveScreenIndex());

                    return;
                }

                //we are visible
            }

            double XBorder = 59;
            double YBorder = 12;

            Point FinalPosition = new Point(ItemOnScreen.X - XBorder, ItemOnScreen.Y - YBorder);

            //try to position it at pointer
            FinalPosition.X -= PreferredPointerOffset;

            if (FinalPosition.X + XBorder < s.Left)
            {
                //offscreen left
                //move pointer instead
                FinalPosition.X = s.Left - XBorder;
                SetPointerPosition(ItemOnScreen.X, true);
            }
            else if (FinalPosition.X + MainGrid.Width - XBorder > s.Right)
            {
                //offscreen right
                //move pointer instead
                FinalPosition.X = s.Right - MainGrid.Width + XBorder;
                double PointerOffset = ItemOnScreen.X - FinalPosition.X - XBorder;
                SetPointerPosition(PointerOffset, true);
            }

            //Correct Y
            if (FinalPosition.Y + MainGrid.Height - YBorder > s.Bottom)
            {
                FinalPosition.Y = ItemOnScreen.Y - MainGrid.Height + YBorder;
                double PointerOffset = ItemOnScreen.X - FinalPosition.X - XBorder;
                SetPointerPosition(PointerOffset, false, true);
            }

            //set position
            this.Left = FinalPosition.X;
            this.Top = FinalPosition.Y;
        }

        private void SetPointerPosition(double Xoffset, bool ShowTopPointer = true, bool ShowBottomPointer = false)
        {
            if (Xoffset < 20)
                Xoffset = 20;

            if (Xoffset > 462)
                Xoffset = 462;

            PointerLeft.Point = new Point(Xoffset - 10.0, PointerLeft.Point.Y);
            PointerCenter.Point = new Point(Xoffset, (ShowTopPointer ? PointerLeft.Point.Y - 10.0 : PointerLeft.Point.Y));
            PointerRight.Point = new Point(Xoffset + 10.0, PointerRight.Point.Y);

            PointerRightBottom.Point = new Point(Xoffset + 10.0, PointerRightBottom.Point.Y);
            PointerCenterBottom.Point = new Point(Xoffset, (ShowBottomPointer ? PointerLeftBottom.Point.Y + 10.0 : PointerLeftBottom.Point.Y));
            PointerLeftBottom.Point = new Point(Xoffset - 10.0, PointerLeftBottom.Point.Y);
        }

        public EditItem(MainWindow MainWindow, SBItem Item)
        {
            InitializeComponent();
            this.KeyDown += new KeyEventHandler(EditItem_KeyDown);
            this.Loaded += new RoutedEventHandler(EditItem_Loaded);

            if (Item.IsFolder)
            {
                PathGrid.IsEnabled = false;
                ArgumentsGrid.IsEnabled = false;
                this.cbAdmin.IsEnabled = false;
            }

            this.NameBox.Text = Item.Name;
            this.PathBox.Text = Item.ApplicationPath;
            this.ArgumentsBox.Text = Item.Arguments;
            this.cbAdmin.IsChecked = Item.RunAsAdmin;

            this.ActiveItem = Item;
            this.ClientWindow = MainWindow;

            //Set Icon Preview
            IconFrame.Source = this.ActiveItem.Icon;

            //backup settings
            PerformBackup(Item);
        }

        private void EditItem_Loaded(object sender, RoutedEventArgs e)
        {
            PositionWindowAtItem(this.ActiveItem, this.ClientWindow);
            (Resources["WindowOpenAnimation"] as Storyboard).Begin();
        }

        private void EditItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                this.Close();
        }

        private void ConfirmClicked(object sender, RoutedEventArgs e)
        {
            if (this.ActiveItem.IsFolder)
            {
                this.ActiveItem.Name = NameBox.Text;
            }
            else
            {
                this.ActiveItem.Name = NameBox.Text;
                this.ActiveItem.ApplicationPath = PathBox.Text;
                this.ActiveItem.Arguments = ArgumentsBox.Text;
                this.ActiveItem.RunAsAdmin = (bool)cbAdmin.IsChecked;
            }

            this.ActiveItem.UpdateIcon();

            this.Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            RestoreBackup(this.ActiveItem);
            this.Close();
        }

        private void ResetIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.ActiveItem.IsFolder)
            {
                this.ActiveItem.Icon = SBItem.FolderIcon;
            }
            else
            {
                this.ActiveItem.Icon = SBItem.LoadingImage;

                try
                {
                    this.ActiveItem.Icon = MiscUtils.GetFileThumbnail(this.ActiveItem.ApplicationPath);
                }
                catch { }
            }

            IconFrame.Source = this.ActiveItem.Icon;

            //restore item path
            this.ActiveItem.IconPath = null;
        }

        private void IconFrame_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //Load new Icon
            OpenFileDialog op = new OpenFileDialog();
            op.Title = TranslationSource.Instance["SelectIcon"];
            op.Filter = "Image files (*.png)|*.png|Image files (*.jpg)|*.jpg|All Files (*.*)|*.*";
            if (op.ShowDialog() == true)
            {
                this.ActiveItem.Icon = SBItem.LoadingImage;

                //try to load the replacement icon
                try
                {
                    this.ActiveItem.Icon = MiscUtils.LoadBitmapImage(op.FileName, 128);
                }
                catch { }

                IconFrame.Source = this.ActiveItem.Icon;

                //Set as IconPath
                this.ActiveItem.IconPath = op.FileName;
            }
        }

        private void ChoosePathButton_Click(object sender, RoutedEventArgs e)
        {
            ChoosePathContextMenu.PlacementTarget = ChoosePathButton;
            ChoosePathContextMenu.IsOpen = true;
        }

        private void FileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            if (op.ShowDialog() == true)
            {
                this.ActiveItem.ApplicationPath = op.FileName;
                this.PathBox.Text = this.ActiveItem.ApplicationPath;

                //only load icon if no replacement icon is set
                if (this.ActiveItem.IconPath == null)
                {
                    this.ActiveItem.Icon = SBItem.LoadingImage;

                    try
                    {
                        this.ActiveItem.Icon = MiscUtils.GetFileThumbnail(this.ActiveItem.ApplicationPath);
                    }
                    catch { }

                    IconFrame.Source = this.ActiveItem.Icon;
                }
            }
        }

        private void FolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog ofd = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = ofd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                this.ActiveItem.ApplicationPath = ofd.SelectedPath;
                this.PathBox.Text = this.ActiveItem.ApplicationPath;

                //only load icon if no replacement icon is set
                if (this.ActiveItem.IconPath == null)
                {
                    this.ActiveItem.Icon = SBItem.LoadingImage;

                    try
                    {
                        this.ActiveItem.Icon = MiscUtils.GetFileThumbnail(this.ActiveItem.ApplicationPath);
                    }
                    catch { }

                    IconFrame.Source = this.ActiveItem.Icon;
                }
            }
        }
    }
}