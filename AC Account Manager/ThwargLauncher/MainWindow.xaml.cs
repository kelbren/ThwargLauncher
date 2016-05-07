﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;
using ThwargLauncher.AccountManagement;
using WindowPlacementUtil;

namespace ThwargLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //private Dictionary<string, List<AccountCharacter>> _allAccountCharacters;
        
        private List<string> _Images = new List<string>();
        private Random _rand = new Random();
        private BackgroundWorker _worker = new BackgroundWorker();
        private string _launcherLocation;

        public static string OldUsersFilePath = Path.Combine(Configuration.AppFolder, "UserNames.txt");
        private MainWindowViewModel _viewModel;
        private WebService.WebServiceManager _webManager = new WebService.WebServiceManager();
        private GameStatusMap _gameStatusMap;
        private Configurator _configurator;
        private GameMonitor _gameMonitor;
        private UiGameMonitorBridge _uiGameMonitorBridge = null;

        private System.Collections.Concurrent.ConcurrentQueue<LaunchItem> _launchConcurrentQueue = 
            new System.Collections.Concurrent.ConcurrentQueue<LaunchItem>();

        public MainWindow()
        {
            // run the updater application if it is where we expect it to be (installation root), and wait for it to finish before loading
            if (System.IO.File.Exists("updater.exe"))
            {
                // if we would like to get the exit code of the updater app and log it later, we have it.
                int exitCode;
                ProcessStartInfo info = new ProcessStartInfo();
                info.Arguments = "/silent";
                info.FileName = "updater.exe";
                using (Process proc = Process.Start(info))
                {
                    proc.WaitForExit();
                    exitCode = proc.ExitCode;
                }
            }

            InitializeComponent();
            _gameStatusMap = new GameStatusMap();
            _viewModel = new MainWindowViewModel(_gameStatusMap);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += _viewModel_PropertyChanged;

            MigrateSettingsIfNeeded();
            EnsureDataFoldersExist();

            LoadUserAccounts(initialLoad: true);
            LoadImages();
            ChangeBackgroundImageRandomly();

            WireUpBackgroundWorker();

            if (Properties.Settings.Default.ACLocation != "")
            {
                txtLauncherLocation.Text = Properties.Settings.Default.ACLocation;
            }
        }
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            //TODO: Implement Web Service Stuff
            BeginMonitoringGame();
            this.Show();
            if (Properties.Settings.Default.ShowHelpAtStart)
            {
                DisplayHelpWindow();
            }
        }
        private void BeginMonitoringGame()
        {
            _configurator = new Configurator();
            RecordGameDll();
            _gameMonitor = new GameMonitor(_gameStatusMap, _configurator);
            _uiGameMonitorBridge = new UiGameMonitorBridge(_gameMonitor, _viewModel);
            _uiGameMonitorBridge.Start();
            _gameMonitor.Start();
            /*
            try
            {
                _webManager.Listen();
            }
            catch (Exception exc)
            {
                Log.WriteError("Exception starting web service: " + exc.Message);
                ShowErrorMessage("Failed to start web service");

            }
             * */
        }
        private void RecordGameDll()
        {
            var info = MagFilter.LaunchControl.GetMagFilterInfo();
            _configurator.AddGameConfig(
                new Configurator.GameConfig()
                    {
                        MagFilterPath = info.MagFilterPath,
                        MagFilterVersion = info.MagFilterVersion
                    }
                );
        }
        private void EndMonitoringGame()
        {
            _uiGameMonitorBridge.Stop();
            _gameMonitor.Stop();
            /*
            try
            {
                _webManager.StopListening();
            }
            catch (Exception exc)
            {
                Log.WriteError("Exception stopping web service: " + exc.Message);
            }
             * */
        }

        void _viewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // TODO - this is a workaround
            if (e.PropertyName == "KnownUserAccounts")
            {
                this.lstUsername.ItemsSource = null;
                this.lstUsername.ItemsSource = _viewModel.KnownUserAccounts;
            }
        }
        private void MigrateSettingsIfNeeded()
        {
            if (Properties.Settings.Default.NeedsUpgrade)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.NeedsUpgrade = false;
                Properties.Settings.Default.Save();
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            LoadWindowSettings();
        }
        private void LoadWindowSettings()
        {
            this.SetPlacement(Properties.Settings.Default.MainWindowPlacement);
        }
        private void SaveWindowSettings()
        {
            Properties.Settings.Default.MainWindowPlacement = this.GetPlacement();
            Properties.Settings.Default.Save();
        }
        private void LoadImages()
        {
            _Images.Clear();
            _Images.Add("acwallpaperwideaerbax.jpg");
            _Images.Add("acwallpaperwideaerfalle.jpg");
            _Images.Add("acwallpaperwideGroup.jpg");
            _Images.Add("acwallpaperwide10yrs.jpg");
        }

        private string PickRandomImage()
        {
            return _Images[_rand.Next(_Images.Count)];
        }

        private void ChangeBackgroundImageRandomly()
        {
            LoadImages();
            string imageName = PickRandomImage();
            //BigGrid.Background = MyTextBlock.Background;
            ImageBrush brush = new ImageBrush(
                new BitmapImage(
                    new Uri("Images\\" + imageName, UriKind.Relative)
                    ));
            ContentGrid.Background = brush;
            
        }

        private void EnsureDataFoldersExist()
        {
            // Ensure program data folder exists
            string specificFolder = Configuration.AppFolder;
            if (!Directory.Exists(specificFolder))
            {
                Directory.CreateDirectory(specificFolder);
            }

            // Ensure profiles folder exists
            var mgr = new ProfileManager();
            mgr.EnsureProfileFolderExists();
        }

        private void LoadUserAccounts(bool initialLoad = false)
        {
            _viewModel.CreateProfileIfDoesNotExist();
            if (!initialLoad) // we do not save the first time, because have never yet loaded
            {
                SaveCurrentProfile();
            }
            ReloadKnownAccountsAndCharacters();
            try
            {
                _viewModel.LoadMostRecentProfile();
            }
            catch
            {
                ShowErrorMessage("Error loading profile");
            }
        }
        private void SaveCurrentProfile()
        {
            _viewModel.SaveCurrentProfile();
        }
        private void ReloadKnownAccountsAndCharacters()
        {
            AccountParser parser = new AccountParser();
            List<UserAccount> accounts = null;
            try
            {
                accounts = parser.ReadOrMigrateAccounts(OldUsersFilePath);
            }
            catch (Exception exc)
            {
                Log.WriteError("Exception reading account file: " + exc.Message);
                accounts = new List<UserAccount>();
            }
            _viewModel.Reset();
            foreach (UserAccount acct in accounts)
            {
                _viewModel.KnownUserAccounts.Add(acct);
            }
            lstUsername.ItemsSource = null;
            lstUsername.ItemsSource = _viewModel.KnownUserAccounts;
        }
        private void btnLaunch_Click(object sender, RoutedEventArgs e)
        {
            _launcherLocation = txtLauncherLocation.Text;
            if (string.IsNullOrEmpty(_launcherLocation))
            {
                ShowErrorMessage("Game launcher location required");
                return;
            }
            if (!File.Exists(_launcherLocation))
            {
                ShowErrorMessage(string.Format("Game launcher missing: '{0}'", _launcherLocation));
                return;
            }
            if (!CheckAccountsAndPasswords())
            {
                return;
            }
            LaunchAllClientsOnAllServersOnThread();
        }
        private bool CheckAccountsAndPasswords()
        {
            int count = 0;
            var accounts = new Dictionary<string, int>();
            int numberBlankPasswords = 0;
            foreach (var account in _viewModel.KnownUserAccounts)
            {
                if (account.AccountLaunchable)
                {
                    accounts[account.Name] = 1;
                    foreach (var server in account.Servers)
                    {
                        if (server.ServerSelected)
                        {
                            ++count;
                            if (string.IsNullOrWhiteSpace(account.Name))
                            {
                                // We try not to let this happen, so it should be very rare
                                ShowErrorMessage("Blank account not allowed");
                                return false;
                            }
                            if (string.IsNullOrWhiteSpace(account.Password))
                            {
                                ++numberBlankPasswords;
                            }
                        }
                    }
                }
            }
            if (accounts.Keys.Count > 1 && numberBlankPasswords > 0)
            {
                ShowErrorMessage("Blank passwords are not allowed when launching multiple accounts");
                return false;
            }
            if (count == 0)
            {
                ShowErrorMessage("No accounts and servers selected");
                return false;
            }
            return true;
        }

        private void WireUpBackgroundWorker()
        {
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;
            _worker.ProgressChanged += _worker_ProgressChanged;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;
        }

        private void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            LoadUserAccounts();
            try
            {
                if (e.Cancelled)
                {
                    lblWorkerProgress.Content = "User Cancelled";
                }
                else if (e.Error != null)
                {
                    lblWorkerProgress.Content = string.Format("Error: {0}", e.Error.Message);
                }
                else
                {
                    lblWorkerProgress.Content = "Launcher Complete";
                }
            }
            finally
            {
                EnableInterface(true);
                btnCancel.IsEnabled = false;
            }
        }
        private class WorkerArgs
        {
            public System.Collections.Concurrent.ConcurrentQueue<LaunchItem> ConcurrentLaunchQueue;
        }
        private void LaunchAllClientsOnAllServersOnThread()
        {
            if (_worker.IsBusy)
            {
                lblWorkerProgress.Content = "Launcher In Use";
            }
            else
            {
                EnableInterface(false);
                btnCancel.IsEnabled = true;
                UpdateConcurrentQueue();
                _viewModel.RecordProfileLaunch();
                WorkerArgs args = new WorkerArgs()
                    {
                        ConcurrentLaunchQueue = _launchConcurrentQueue
                    };
                _worker.RunWorkerAsync(args);
            }
        }
        private void UpdateConcurrentQueue()
        {
            var launchSorter = new LaunchSorter();
            var launchList = GetLaunchListFromAccountList(_viewModel.KnownUserAccounts);
            launchList = launchSorter.SortLaunchList(launchList);
            foreach (var item in launchList.GetLaunchList())
            {
                _launchConcurrentQueue.Enqueue(item);
            }
            _gameMonitor.QueueReread();
        }
        private void EnableInterface(bool enable)
        {
            btnLaunch.IsEnabled = enable;
        }
        /// <summary>
        /// Get all server/accounts that are checked to be launched
        /// AND that are not already running
        /// </summary>
        /// <param name="accountList"></param>
        /// <returns></returns>
        private LaunchSorter.LaunchList GetLaunchListFromAccountList(IEnumerable<UserAccount> accountList)
        {
            var launchList = new LaunchSorter.LaunchList();
            foreach (var account in accountList)
            {
                if (account.AccountLaunchable)
                {
                    foreach (var server in account.Servers)
                    {
                        if (server.ServerSelected)
                        {
                            if (_gameStatusMap.HasGameStatusByServerAccount(serverName: server.ServerName, accountName: account.Name))
                            {
                                continue;
                            }
                            var launchItem = new LaunchItem()
                                {
                                    AccountName = account.Name,
                                    Priority = account.Priority,
                                    Password = account.Password,
                                    ServerName = server.ServerName,
                                    CharacterSelected = server.ChosenCharacter,
                                    CustomLaunchPath = account.CustomLaunchPath,
                                    CustomPreferencePath = account.CustomPreferencePath
                                };
                            launchList.Add(launchItem);
                        }
                    }
                }
            }
            return launchList;
        }

        private class ProgressInfo { public int Index; public int Total; public string Message; }
        void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressInfo info = (e.UserState as ProgressInfo);
            lblWorkerProgress.Content = string.Format(
                "{0}% - {1}/{2} - {3}",
                e.ProgressPercentage,
                info.Index, info.Total,
                info.Message
                );
        }

        void workerReportProgress(string verb, LaunchItem launchItem, int index, int total)
        {
            int pct = (int)(100.0 * index / total);
            string context = string.Format(
                "{0} {1}:{2}",
                verb, launchItem.AccountName, launchItem.ServerName);
            var progressInfo = new ProgressInfo()
                {
                    Index = index,
                    Total = total,
                    Message = context
                };
            _worker.ReportProgress(pct, progressInfo);
        }
        void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            WorkerArgs args = (e.Argument as WorkerArgs);
            if (args == null) { return; }
            int serverIndex = 0;
            System.Collections.Concurrent.ConcurrentQueue<LaunchItem> globalQueue = args.ConcurrentLaunchQueue;
            int serverTotal = globalQueue.Count;

            LaunchItem launchItem = null;
            var accountLaunchTimes = new Dictionary<string, DateTime>();

            while (globalQueue.TryDequeue(out launchItem))
            {    
                LaunchManager mgr = new LaunchManager(_launcherLocation, launchItem, accountLaunchTimes);
                mgr.ReportStatusEvent += (status, item) => HandleLaunchMgrStatus(status, item, serverIndex, serverTotal);
                var launchResult = mgr.LaunchGameHandlingDelaysAndTitles(_worker);
                
                if (launchResult.Success)
                {
                    UpdateAccountStatus(true, launchItem);
                    ++serverIndex;
                    workerReportProgress("Launched", launchItem, serverIndex, serverTotal);
                    // TODO
                    // record game info
                    // launchItem's server, account, character
                    // and launchResult.ProcessId
                }
                else
                {
                    UpdateAccountStatus(false, launchItem);
                    globalQueue.Enqueue(launchItem);
                    workerReportProgress("Requeued", launchItem, serverIndex, serverTotal);
                }

                if (_worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

            }
        }

        private void UpdateAccountStatus(bool success, LaunchItem launchItem)
        {
            _viewModel.updateAccountStatus(success, launchItem.ServerName, launchItem.AccountName);
        }

        private void HandleLaunchMgrStatus(string status, LaunchItem launchItem, int serverIndex, int serverTotal)
        {
            workerReportProgress(status, launchItem, serverIndex, serverTotal);
        }

        public static void ShowErrorMessage(string msg)
        {
            ShowMessage(msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        public static void ShowMessage(string msg)
        {
            ShowMessage(msg, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public static void ShowMessage(string msg, string caption, MessageBoxButton button, MessageBoxImage image)
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(msg, caption, button, image));
        }

        private void OpenLauncherLocation()
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.InitialDirectory = "C:\\Turbine\\Asheron's Call";

            // Set filter for file extension and default file extension
            dlg.DefaultExt = ".exe";
            dlg.Filter = "Executables (exe)|*.exe|All files (*.*)|*.*";

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                string filename = dlg.FileName;
                txtLauncherLocation.Text = filename;
            }
        }

        private void txtLauncherLocation_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenLauncherLocation();
        }

        private void btnLauncherLocation_Click(object sender, RoutedEventArgs e)
        {
            OpenLauncherLocation();
        }

        private void ThwargLauncherMainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EndMonitoringGame();
            SaveWindowSettings();
            SaveCurrentProfile();

            Properties.Settings.Default.SelectedUser = lstUsername.SelectedIndex;
            Properties.Settings.Default.ACLocation = txtLauncherLocation.Text;
            
            Properties.Settings.Default.Save();
        }

        private void btnAddUsers_Click(object sender, RoutedEventArgs e)
        {
            MainWindowDisable();
            var dlg = new AddUsers(_viewModel.KnownUserAccounts);
            dlg.ShowDialog();
            LoadUserAccounts();
            MainWindowEnable();
        }
        private void btnChooseProfile_Click(object sender, RoutedEventArgs e)
        {
            MainWindowDisable();
            var dlg = new ChooseProfile();
            var result = dlg.ShowDialog();
            MainWindowEnable();
            if (result.HasValue && result.Value && !string.IsNullOrEmpty(dlg.ProfileNameChosen))
            {
                string desiredProfileName = dlg.ProfileNameChosen;
                _viewModel.GotoSpecificProfile(desiredProfileName);
            }
            else
            {
                _viewModel.LoadMostRecentProfile();
            }
        }
        private void MainWindowDisable()
        {
            rctBlack.Fill = new SolidColorBrush(Colors.Gainsboro);
        }
        private void MainWindowEnable()
        {
            rctBlack.Fill = new SolidColorBrush(Colors.Black);
        }
        private void btnOpenUsers_Click(object sender, RoutedEventArgs e)
        {
            MainWindowDisable();

            var startInfo = new ProcessStartInfo("notepad", AccountParser.AccountFilePath);
            var notepadProcess = new Process() {StartInfo = startInfo};
            if (notepadProcess.Start())
            {
                notepadProcess.WaitForExit();
            }
            LoadUserAccounts();
            MainWindowEnable();
        }
        private void btnEditUsers_Click(object sender, RoutedEventArgs e)
        {
            AccountEditorViewModel acevm = new AccountEditorViewModel(this._viewModel.KnownUserAccounts);
            
            AccountEditor dlg = new AccountEditor();
            dlg.DataContext = acevm;
            dlg.ShowDialog();
            LoadUserAccounts();
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e)
        {
            DisplayHelpWindow();
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            btnCancel.IsEnabled = false;
            _worker.CancelAsync();
        }
        private void DisplayHelpWindow()
        {
            MainWindowDisable();
            var dlg = new HelpWindow(new HelpWindowViewModel(_configurator));
            dlg.ShowDialog();
            MainWindowEnable();
        }

        private void txtProfileName_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            btnChooseProfile_Click(sender, e);
        }
    }
}
