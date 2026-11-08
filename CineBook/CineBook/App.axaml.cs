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
using Avalonia.Media;

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
        Exception? startupException = null;
        if (!Design.IsDesignMode)
        {
            try
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
            catch (Exception exception)
            {
                startupException = exception;
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            if (startupException is not null)
                mainWindow.Content = CreateStartupErrorPage(startupException);
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new PageNavigationHost()
            {
                Page = startupException is null
                    ? new MainView { DataContext = new MainViewModel() }
                    : CreateStartupErrorPage(startupException)
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new PageNavigationHost()
            {
                Page = startupException is null
                    ? new MainView { DataContext = new MainViewModel() }
                    : CreateStartupErrorPage(startupException)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ContentPage CreateStartupErrorPage(Exception exception)
    {
        string databasePath;
        try
        {
            databasePath = CineBookContext.GetDatabasePath();
        }
        catch (Exception pathException)
        {
            databasePath = pathException.ToString();
        }

        var details = new TextBox
        {
            Text = $"Database path:\n{databasePath}\n\n{exception}",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 280
        };

        return new ContentPage
        {
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Margin = new Thickness(24),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Не удалось запустить CineBook",
                            FontSize = 22,
                            FontWeight = FontWeight.Bold
                        },
                        new TextBlock
                        {
                            Text = "Скопируйте текст ошибки и отправьте его для диагностики.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        details
                    }
                }
            }
        };
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
