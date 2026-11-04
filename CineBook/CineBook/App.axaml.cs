using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CineBook.ViewModels;
using CineBook.Views;
using CineBook.Context;
using CineBook.Models;
using Microsoft.EntityFrameworkCore;
using Avalonia.Controls;

namespace CineBook;

public partial class App : Application
{
    private const string ThemeSettingKey = "app.theme";
    private bool _themePersistenceReady;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (!Design.IsDesignMode)
        {
            CineBookContext.InitializeDatabaseFromBundledTemplate();
            using var context = new CineBookContext();
            context.Database.EnsureCreated();

            var savedTheme = context.AppSettings.Find(ThemeSettingKey)?.Value;
            RequestedThemeVariant = savedTheme switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
            _themePersistenceReady = true;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new PageNavigationHost()
            {
                Page = new MainView { DataContext = new MainViewModel() }
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new PageNavigationHost()
            {
                Page = new MainView { DataContext = new MainViewModel() }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_themePersistenceReady && change.Property == RequestedThemeVariantProperty &&
            RequestedThemeVariant is { } theme && (theme == ThemeVariant.Light || theme == ThemeVariant.Dark))
            SaveThemePreference(theme);
    }

    private static void SaveThemePreference(ThemeVariant theme)
    {
        try
        {
            using var context = new CineBookContext();
            var setting = context.AppSettings.Find(ThemeSettingKey);
            if (setting is null)
            {
                setting = new AppSetting { Key = ThemeSettingKey };
                context.AppSettings.Add(setting);
            }

            setting.Value = theme == ThemeVariant.Dark ? "Dark" : "Light";
            context.SaveChanges();
        }
        catch (Exception exception)
        {
            if (MainView.Current is not null)
                MainView.Current.ShowToast(exception.InnerException?.Message ?? exception.Message,
                    NotificationType.Error);
        }
    }
}
