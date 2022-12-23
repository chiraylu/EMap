﻿using AvalonDock;
using EM.GIS.Controls;
using EM.IOC;
using System.Windows;
using System;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using EM.GIS.WPFControls.ViewModels;
using EM.WpfBases;

namespace EM.GIS.WPFControls
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        private IIocManager IocManager { get; }
        private MainWindowViewModel ViewModel { get; set; }
        public MainWindow(IIocManager iocManager)
        {
            InitializeComponent();
            IocManager= iocManager ?? throw new ArgumentNullException(nameof(iocManager));
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var appManager = IocManager.GetService<IWpfAppManager>();
            if (appManager == null)
            {
                throw new Exception($"未注册{nameof(IWpfAppManager)}");
            }
            appManager.Map = map;
            appManager.Map.Legend = legend;
            appManager.Legend = legend;
            appManager.Ribbon = ribbon;
            appManager.StatusBar = statusBar;
            appManager.DockingManager = dockingManager;
            ViewModel = new MainWindowViewModel(this, appManager, IocManager);

            DataContext = ViewModel;
            if (ViewModel.IocManager != null)
            {
                ViewModel.IocManager.LoadPlugins();
            }
        }
     
    }
}
