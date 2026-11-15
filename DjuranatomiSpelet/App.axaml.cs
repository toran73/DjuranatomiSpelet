// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DjuranatomiSpelet.Data;

namespace DjuranatomiSpelet;

public partial class App : Application
{
    public const string GameName = "Djuranatomi Spelet";

    private static SqliteGameRepository? _repository;

    /// <summary>The database (created with an example animal on first use).</summary>
    public static SqliteGameRepository Repository =>
        _repository ??= new SqliteGameRepository(SqliteGameRepository.DefaultPath());

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = new SetupWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
