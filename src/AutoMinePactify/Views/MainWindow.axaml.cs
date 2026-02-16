using System;
using Avalonia.Controls;
using AutoMinePactify.ViewModels;

namespace AutoMinePactify.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Dispose();
        }
        base.OnClosed(e);
    }
}
