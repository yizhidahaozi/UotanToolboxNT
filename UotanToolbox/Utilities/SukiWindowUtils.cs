using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using SukiUI.Controls;

namespace UotanToolbox.Utilities
{
    public static class SukiWindowUtils
    {
        /// <summary>
        /// For the record this is gross and all the theme, background, toasts and dialog stack needs to be rewritten.
        /// Providing each of them via discrete services with only a service locator singleton is a better long term strategy.
        /// </summary>
        public static SukiWindow Get()
        {
            var app = Application.Current ?? throw new InvalidOperationException("Application.Current is null");
            if (app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
                throw new InvalidOperationException("Application lifetime is not a classic desktop style lifetime");
            if (lifetime.MainWindow is not SukiWindow win)
                throw new InvalidOperationException("MainWindow is not initialized or not a SukiWindow");
            return win;
        }

        /// <summary>
        /// I hate this too.
        /// </summary>
        public static SukiToast GetSukiHost(this SukiWindow window)
        {
            var toast = window.FindDescendantOfType<SukiToast>();
            if (toast == null)
                throw new InvalidOperationException("SukiToast not found in window visual tree");
            return toast;
        }
    }
}